# Изолированная приёмка Docker/nginx

Требуются Docker Compose, .NET 10 и Python 3. Стек использует настоящую Production-сборку,
PostgreSQL 16, nginx 1.24, непривилегированного пользователя и именованные volumes.
Опубликованы только loopback-порты 5291/5292. Это HTTP-стенд с `X-Forwarded-Proto: https`,
а не проверка TLS/CDN или конфигурации рабочего nginx.

```sh
docker compose -f tests/production-like/docker-compose.yml up -d --build
dotnet publish tests/production-like/Fixture.csproj -c Release -o /tmp/chat-fixture
docker cp /tmp/chat-fixture relaxerr-chat-acceptance-web-1:/tmp/fixture
docker compose -f tests/production-like/docker-compose.yml exec -T web \
  dotnet /tmp/fixture/Fixture.dll > /tmp/chat-manifest.json
docker compose -f tests/production-like/docker-compose.yml exec -T nginx nginx -t
python3 tests/production-like/smoke.py /tmp/chat-manifest.json
docker compose -f tests/production-like/docker-compose.yml restart web
python3 tests/production-like/smoke.py /tmp/chat-manifest.json
```

Дождитесь готовности HTTP после старта/restart; при необходимости повторите только smoke.
Fixture отказывается работать без `DND_ACCEPTANCE_FIXTURE=1` и точного имени БД
`relaxerr_acceptance`, отказывается заменять существующих пользователей. Создаются
`p0@acceptance.invalid` (Admin), `p1`–`p4` (Player), пароль `Acceptance123!Local`.
Эти общедоступные тестовые значения нельзя использовать вне изолированного стенда.
Fixture вызывает настоящий ChatService под пользователем контейнера: сохраняет приватное
вложение 100 000 000 байт и проверяет отказ на 100 000 001. **Загрузка fixture обходит nginx**;
smoke проверяет через nginx вход, страницы, скачивание, права, заголовки и Foundry API.

Для проверки реального InputFile создайте два файла:

```sh
python3 - <<'PY'
for name, size in [('boundary-ui.bin', 100_000_000), ('oversize-ui.bin', 100_000_001)]:
    with open('/tmp/' + name, 'wb') as file:
        file.truncate(size)
PY
```

В браузере на `http://localhost:5291/chat` войдите p1, выберите первый файл и отправьте.
Дождитесь ссылки с точным размером, скачайте. Выбор второго файла должен показать
«Лимит файла — 100 000 000 байт.» до создания сообщения. Проверьте число сообщений в БД.

Для двух клиентов откройте p1 в Chrome на 5291, p2 в Safari на 5292. Остановите только
первый proxy: `docker compose -f tests/production-like/docker-compose.yml stop nginx`.
Дождитесь экрана reconnect первого клиента; во втором измените/удалите свои сообщения,
добавьте новое. Выполните `start nginx`, дождитесь автоматического reconnect, убедитесь,
что изменения видны без дублей. Отдельно повторите с личной веткой и отозванным stamp.
Остановка одного proxy не меняет сеть компьютера или доступ второго клиента.

Проверка физической уборки и повторного запуска:

```sh
docker compose -f tests/production-like/docker-compose.yml exec -T web dotnet /tmp/fixture/Fixture.dll expire
docker compose -f tests/production-like/docker-compose.yml restart web
python3 tests/production-like/smoke.py /tmp/chat-manifest.json --expired
```

После `up --force-recreate web` скопируйте fixture заново: `/tmp` контейнера намеренно
не постоянный. Данные файлов и БД должны оставаться в именованных volumes.
Для остановки — `down` без `-v`. Повторный чистый запуск fixture требует **только тестовых**
volumes: их удаление допустимо лишь для проекта `relaxerr-chat-acceptance`, никогда prod.

Фактически выполненные проверки и ограничения — [chat-acceptance.md](../../docs/chat-acceptance.md).
