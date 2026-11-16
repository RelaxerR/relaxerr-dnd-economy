# Чат и планирование: эксплуатация

Модуль использует действующие cookie Identity, роли Admin/Player и PostgreSQL. Foundry API-ключи, CORS и формулы экономики не меняются. Поддерживаемая конфигурация интерфейса — один экземпляр Blazor Server; горизонтальное масштабирование требует отдельно настроить sticky sessions и общие Data Protection keys.

## Локальная сборка и проверки

Требуются .NET SDK 10 и PostgreSQL 16 с ICU и `pg_trgm`. Тестовый PostgreSQL-пользователь должен иметь CREATEDB. Интеграционный тест сам создаёт случайную БД `relaxerr_chat_test_<guid>`, мигрирует её и удаляет в `finally`. Не указывайте рабочую или продакшен-БД.

```sh
dotnet restore
dotnet build --configuration Release
DND_TEST_POSTGRES='Host=localhost;Database=postgres;Username=postgres;Password=local-test-password' \
  dotnet test --configuration Release --logger 'trx;LogFileName=tests.trx'
```

Без `DND_TEST_POSTGRES` интеграционная проверка явно пропускается, а не считается выполненной. CI `Build and test` поднимает одноразовый PostgreSQL 16 и запускает также её. Тесты не используют реальную push-службу: запросы перехватывает HTTP-handler, а подпись и шифрование выполняются настоящей библиотекой WebPush.

```sh
dotnet tool install --global dotnet-ef --version 10.0.10
dotnet ef migrations has-pending-model-changes \
  --project src/DndEconomy.Infrastructure --startup-project src/DndEconomy.Web
dotnet ef migrations script --idempotent \
  --project src/DndEconomy.Infrastructure --startup-project src/DndEconomy.Web \
  --output /tmp/chat-migrations.sql
```

`DesignTimeDbContextFactory` не запускает приложение, фоновые обработчики и сидинг. Для применения миграций EF CLI задайте `ConnectionStrings__Default` явно. На обычном запуске миграции по-прежнему применяются существующим startup-процессом. Новые миграции добавляют таблицы и `EconomySessions.IsCancelled=false`, затем security stamp подписки и CHECK «отменённое не закреплено». Старые строки не удаляются и не заменяются. Перед обновлением окружения нужны обычный backup PostgreSQL и копия файлового volume; откат схемы удалит новые данные модуля, поэтому предпочтителен исправляющий релиз.

## Файлы и reverse proxy

`Chat__StoragePath` — абсолютный путь к приватному каталогу, доступному процессу для записи. По умолчанию `private-chat-files` относительно рабочего каталога. В compose уже задан `/data/chat-files` и постоянный volume `chatfiles`. Не размещайте его в `wwwroot`, не добавляйте nginx alias и публичные правила CDN. Backup должен включать БД и этот volume. `docker compose down -v` уничтожит данные, поэтому для обычного перезапуска используйте `restart` или `up -d`.

Dockerfile создаёт `/data/chat-files` с владельцем `app` (UID 1654) до перехода на непривилегированного
пользователя. Новый named volume наследует эти права. **Существующий** volume с владельцем root
при обновлении образа не исправляется автоматически. Проверить:

```sh
docker compose -f docker-compose.prod.yml exec -T web sh -c 'test -w /data/chat-files && echo "Chat storage writable"'
```

Если каталог не доступен, после backup выполнить одноразово для этого volume:

```sh
docker compose -f docker-compose.prod.yml exec -T --user 0 web sh -c 'chown app:app /data/chat-files'
```

Не менять права всего `/data` и не переключать рабочее приложение на root.
Воспроизводимый изолированный Docker/nginx-стенд и точные границы проверок:
[tests/production-like/README.md](../tests/production-like/README.md). CI запускает его отдельным job.

Файлы передаются через поток `InputFile` маленькими сообщениями существующего Blazor circuit; 100 МБ не буферизуются в памяти сервера и не отправляются одним multipart POST. Сервер считает фактически прочитанные байты, разрешает 100 000 000 и отклоняет следующий байт. Скачивание — только `/chat-files/<guid>`, `attachment`, `application/octet-stream`, `nosniff`, `private, no-store`, без range-кэширования.

В существующем HTTPS server-блоке nginx сохраните поддержку WebSocket. Пример параметров location приложения (это образец для проверки в staging, не выполненный продакшен-деплой):

```nginx
client_max_body_size 101m;
location / {
    proxy_pass http://127.0.0.1:5041;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 3600s;
    proxy_send_timeout 3600s;
    proxy_request_buffering off;
}
```

Проверить конфигурацию через `nginx -t` до reload. Сопоставьте также лимиты внешнего прокси/CDN. Путь service worker `/chat-sw.js` должен обслуживаться с корня сайта по HTTPS; не добавляйте ему обработчик кэширования приватных страниц. В приложении для чата, календаря, вложений и уведомлений выставляется `private, no-store`.

Фоновый обработчик каждую минуту удаляет просроченные файлы и старые heartbeat, проверяет расписание и создаёт напоминания. Просроченный файл недоступен сразу по времени БД независимо от того, успела ли пройти уборка. Ошибка удаления логируется, проход повторится. Неполные/осиротевшие файлы удаляются через сутки; строки сообщений сохраняются. Архив веток вычисляется из времени активности, отдельного cron нет.

## Web Push

Production использует существующий `.env` рядом с `/opt/relaxerr-dnd-economy/docker-compose.prod.yml`.
GitHub Secrets содержат только параметры SSH-деплоя; VAPID туда дублировать не нужно.
Compose уже отображает `WEB_PUSH_*` в `WebPush__*` для контейнера `web`.
Приватный ключ никогда не передаётся браузеру: `IPushService.PublicKey` возвращает только
публичную часть, и только если вся конфигурация валидна.

| Переменная в серверном `.env` | Конфигурация приложения | Назначение |
|---|---|---|
| `WEB_PUSH_SUBJECT` | `WebPush:Subject` | Реальный `mailto:`-контакт владельца либо HTTPS-адрес сайта |
| `WEB_PUSH_PUBLIC_KEY` | `WebPush:PublicKey` | Публичный ключ P-256 для браузера |
| `WEB_PUSH_PRIVATE_KEY` | `WebPush:PrivateKey` | Секрет P-256, только на сервере |

### Первичная настройка production

1. Сначала дождитесь слияния исправлений в `main`, зелёного CI и штатного Deploy.
   Доступ: `deploy@77.110.104.211`, порт 22, авторизация существующим SSH-ключом.
   Не передавайте приватный SSH-ключ или `.env` в чат. Проверить канал до деплоя:
   `nc -vz -w 5 77.110.104.211 22`, затем
   `ssh -o BatchMode=yes -o ConnectTimeout=10 deploy@77.110.104.211 true`.
   Успех TCP недостаточен: таймаут banner exchange происходит ещё до проверки ключа.
   Если SSH не получает приветствие, через консоль провайдера проверить `systemctl status ssh`,
   `ss -ltnp`, `ufw status`, правила провайдера/fail2ban и журнал `journalctl -u ssh`.
   Не открывать лишние порты и не менять firewall вслепую.
2. На сервере проверьте **только наличие** трёх переменных, не их значения. Например:

   ```sh
   cd /opt/relaxerr-dnd-economy
   python3 - <<'PY_CHECK'
   from pathlib import Path
   names = ('WEB_PUSH_SUBJECT', 'WEB_PUSH_PUBLIC_KEY', 'WEB_PUSH_PRIVATE_KEY')
   rows = [line.partition('=') for line in Path('.env').read_text().splitlines()]
   for name in names:
       values = [value.strip().strip("\"'") for key, sep, value in rows if sep and key.strip() == name]
       print(name + ': ' + ('SET' if len(values) == 1 and values[0] else 'MISSING_OR_DUPLICATE'))
   PY_CHECK
   ```

   Если рабочая пара уже существует, **не генерировать новую**: сначала проверить передачу
   Compose, контакт и диагностику в `/chat-settings`. Частичную конфигурацию восстановить из
   секретного backup, если он есть. Смена пары требует переподписки всех устройств.
3. Только если пары ещё нет, на доверенной машине с .NET 10 и актуальным checkout выполните:

   ```sh
   dotnet run --project tools/DndEconomy.Vapid -- \
     --output /private/path/web-push.env \
     --subject https://relaxerr-dnd-economy.ru
   ```

   Каталог должен существовать и быть приватным. Можно заменить HTTPS subject реальным
   `mailto:`-контактом. Генератор создаёт **новый** файл `0600`, не печатает ключи и отказывается
   перезаписывать существующий файл. На Windows ограничьте ACL владельцем. Это не команда для CI.
   Передайте файл на сервер по защищённому SSH/SCP-каналу, сохранив права `0600`.
4. На сервере локальным редактором перенесите три значения в существующий `.env`, удалив пустые
   дубли этих же имён. Остальные настройки не заменять. `chmod 600 .env`.
   Не выводить `cat .env`, `docker inspect`, `docker compose config` или `printenv` в общий лог.
   Секретный файл генератора убрать из рабочего каталога после переноса и сохранить защищённый backup пары.
5. Применить **изменение окружения**, а не только restart:

   ```sh
   docker compose -f docker-compose.prod.yml up -d --no-deps --force-recreate web
   docker compose -f docker-compose.prod.yml exec -T web sh -c '
     test -n "$WebPush__Subject" && test -n "$WebPush__PublicKey" && test -n "$WebPush__PrivateKey" && echo "VAPID variables present"
   '
   ```

   Эта проверка не печатает значений. Приложение дополнительно проверяет формат и соответствие
   ключей, а также subject. В `/chat-settings` должна появиться кнопка подключения вместо ошибки.
   При отсутствии конфигурации страница теперь перечисляет **имена** недостающих параметров;
   при повреждённой паре объясняет ошибку без секретных значений. Worker не отправляет очередь
   с невалидной конфигурацией и пишет только безопасную диагностику.
6. Проверить HTTPS, вход, service worker, подписку и реальную доставку по списку ниже.
   Записать дату, версию приложения, результаты и остаток приёмки в `docs/chat-acceptance.md`.
   Наличие ключей само по себе не подтверждает доставку.

### Проверка после настройки

- Анонимный `curl -I https://relaxerr-dnd-economy.ru/chat-sw.js`: 200, JavaScript,
  `Cache-Control: no-cache`; scope — корень. Worker не кэширует страницы или вложения.
- В DevTools → Application → Service Workers: `/chat-sw.js` активен. Сохранить типы событий,
  затем нажать подключение. Permission запрашивается непосредственно нажатием пользователя;
  отказ не должен создавать подписку. После разрешения — сообщение «Push подключён».
- С другого аккаунта проверить message/private/thread/mention/poll/request/assigned/cancelled,
  закрыв/свернув принимающую вкладку, затем оба reminder. На блокировке — общий тип события,
  без текста и имён; нажатие открывает нужный чат, где заново проверяется Identity и доступ.
- Убедиться, что серверная подписка появилась (считать строки, не выгружать endpoint/ключи).
  Уведомление открывает `/chat?message=...` либо `/chat?room=...`. Авторизация остаётся обычной.
- Настоящий 410 не имитировать путём отправки на чужой URL: удаление после 410, отказ чужому
  аккаунту и неподдерживаемым схемам проверены изолированным автотестом.
- Android Chrome и установленный на домашний экран iPhone проверять физически: подписка,
  отказ/повторное разрешение, фон/блокировка, переход, отключение. Desktop/mocks их не заменяют.

На Android: войти, открыть «Настройки и уведомления», выбрать типы событий, сохранить, нажать подключение и разрешить уведомления. На iPhone: открыть HTTPS-сайт, «Поделиться» → «На экран Домой», запустить с домашнего экрана и затем подключить push. Требование установки и непосредственного пользовательского нажатия описано [WebKit](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/). Обработка протокола использует [WebPush для .NET](https://github.com/web-push-libs/web-push-csharp).

Исходящий HTTPS разрешается к `fcm.googleapis.com`, `updates.push.services.mozilla.com`, `*.push.apple.com`, `*.notify.windows.com`; endpoint других доменов отклоняется для защиты от SSRF. Endpoint и ключи подписок — приватные данные БД, не включайте их в логи. Настройки событий общие для уведомлений сайта и push. Право администратора читать личные сообщения не подписывает его на их уведомления.

Перед сменой аккаунта на общем устройстве отключите push в прежнем аккаунте. Устройство нельзя перепривязать к чужому аккаунту простым предъявлением endpoint. Смена security stamp (например, принудительный сброс доступа) прекращает доставку прежней подписке. Напоминания за 24 часа и час имеют отдельные ключи; после простоя сервера они догоняются, пока игра ещё не началась. При долгом простое уже начавшаяся игра не напоминается.

## Диагностика

- Нет доступа после reconnect: проверить актуальный аккаунт, MustChangePassword, lockout, security stamp, роли и область личной ветки; повторить вход. Не расширять область чата ради устранения отказа.
- Нет вариантов: открыть предварительный подбор мастера. Красный означает недостаточный состав/неполное окно мастера, оранжевый — требуется подтверждение календарём; они не участвуют в голосовании.
- Голосование ждёт: посмотреть число ответов. Пустой календарь не исключает активного игрока из фиксированного списка; «Ни один вариант» считается ответом.
- Связанная сессия отменена: она остаётся в истории, цены берутся из другой допустимой сессии. Повторное сохранение ссылки старой игры не создаёт новую связь. Обычные ручные сессии работают как прежде.
- Push не приходит: проверить HTTPS/установку/permission, настройки события, соответствие VAPID-пары и исходящий доступ. 404/410 удаляют подписку; временные ошибки повторяются с backoff. Посмотреть `ChatNotice` и `ChatPushDelivery`, не выгружая ключи подписок.
- Ошибки фоновой обработки ищутся по сообщениям «фоновые задачи чата» и «доставка push». Транзакционные блокировки автоматически освобождаются при разрыве соединения; отдельный lock-файл не нужен.
