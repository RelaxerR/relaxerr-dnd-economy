# Relaxerr — Экономика ДнД

Веб-сервис экономики для игроков DnD-кампании: каталог предметов с динамической ценой,
заявки на добавление предметов, профиль игрока, админ-панель. Полное ТЗ и разбор исходной
Excel-модели — см. историю в claude.ai (архив чата у пользователя), здесь — выжимка решений.

## Статус

Фаза 0 и Фаза 1 выполнены, `dotnet build` проходит чисто (0 ошибок, 0 предупреждений)
на .NET 10. Domain и Infrastructure подключают ASP.NET Core Identity через
`<FrameworkReference Include="Microsoft.AspNetCore.App" />` (нужен для `IdentityUser<Guid>`
и `AddSignInManager()` — их нет в отдельных NuGet-пакетах Identity.Core/Stores, только
в shared framework).

Фаза 1 добавила: cookie-аутентификацию поверх `AddIdentityCore` (`AddAuthentication().
AddIdentityCookies()` + `AddAuthorizationCore()` с secure-by-default `FallbackPolicy`),
Blazor-страницы `Components/Account/*` (Login/Lockout/ForceChangePassword/AccessDenied/
InvalidUser, статичный SSR-рендер через `[ExcludeFromInteractiveRouting]` — иначе
`SignInManager` не может писать cookie из интерактивного компонента), идемпотентный сидинг
ролей и первого администратора (`IdentitySeeder`, секция конфига `AdminSeed`), три EF-миграции
(`InitialCreate`, `AddTrigramSearchIndexes`, `AddCyrillicCollationToItemName`) и каталог
с bulk-расчётом цены и опечатко-устойчивым поиском (`CatalogReadStore`/`CatalogQueryService`).
Подробности решений — в разделах ниже.

## Зафиксированные архитектурные решения

- Стек: .NET 10, ASP.NET Core, **Blazor Server** (не SPA), PostgreSQL, EF Core 10, Serilog.
- Модульный монолит: `Domain` → `Application` → `Infrastructure` → `Web`. Не микросервисы —
  один разработчик, лишняя инфраструктура не нужна.
- Авторизация: **только по приглашению админа**, самостоятельной регистрации нет.
- Кампания: **одна активная на сайт** (модель это допускает расширить позже без переписывания).
- Деплой (2026-08-09): сервер 77.110.104.211, Ubuntu 24.04, на нём уже крутятся 4 других
  приложения (Docker) под общим хостовым nginx (не в контейнере) — reverse proxy per-domain
  через `/etc/nginx/sites-available/*`, TLS через Certbot. Наше приложение и Postgres — тоже в
  Docker, каждый в своём контейнере, отдельная compose-сеть `internal` (docker-compose.prod.yml
  в корне репозитория). `db` без публикации порта на хост — виден только внутри `internal`.
  `web` публикует порт только на `127.0.0.1:5041` (не наружу) — nginx проксирует на него по
  loopback, домен `relaxerr-dnd-economy.ru`. Образ собирается прямо на сервере при деплое
  (`dotnet publish` внутри `mcr.microsoft.com/dotnet/sdk:10.0`, рантайм —
  `mcr.microsoft.com/dotnet/aspnet:10.0`), не через registry — соло-проект, лишний GHCR не нужен.
  Автодеплой — GitHub Actions (`.github/workflows/deploy.yml`) на `push` в `main`: SSH под
  сервисным пользователем `deploy` (не root, но в группе `docker` — де-факто эквивалент root на
  демоне, это ограничение самого Docker) запускает `deploy.sh` (`git reset --hard origin/main` +
  `docker compose up -d --build`). Секреты (`ConnectionStrings__Default`, `AdminSeed__*`) — в
  `.env` рядом с `docker-compose.prod.yml` на сервере, не в git (см. `.env.example`).
  Из-за Docker NAT приложение за nginx нуждается в `UseForwardedHeaders` (`Program.cs`,
  `KnownNetworks` = весь диапазон дефолтных docker-бриджей 172.16.0.0/12 + loopback) — без
  этого `RemoteIpAddress` внутри контейнера всегда был бы IP шлюза docker-сети, один и тот же
  для всех посетителей, и rate limiter (партиционирование по IP, см. выше) схлопнулся бы в
  общий счётчик на всех сразу. Миграции применяются при каждом старте контейнера
  (`DatabaseMigrationExtensions.MigrateDatabaseAsync()`, идемпотентно) — отдельного шага
  "прогнать миграции" в пайплайне деплоя нет. DNS (A-записи `@`/`www` → `77.110.104.211`,
  Aeza) и HTTPS (`certbot --nginx`, автопродление сертификата через systemd-таймер certbot)
  настроены в тот же день — сайт живёт на `https://relaxerr-dnd-economy.ru` с редиректом
  с HTTP.
- DDoS-защита: встроенный `RateLimiter` (глобальный sliding window 100/мин на IP + строгий
  fixed window 30/мин на IP для `/Account/Login`) как первый рубеж, Cloudflare перед доменом —
  как второй, более надёжный. Проверено вручную под нагрузкой (`ConfigureRateLimiting` в
  `Program.cs`) — burst-запросы к `/Account/Login` корректно получают 429 после 30-го запроса
  в минуту, к прочим маршрутам — после 100-го (лимиты общие на IP, счётчик делится между
  политиками). **Известное ограничение**: `UseRateLimiter()` защищает только HTTP-конвейер —
  начальную загрузку страницы, статику, установление SignalR-соединения. После того как circuit
  Blazor Server установлен (WebSocket), дальнейшие взаимодействия (клики, поиск, избранное)
  идут через уже открытое соединение и НЕ проходят через этот middleware повторно на каждое
  взаимодействие — значит спам-клики внутри уже открытого circuit'а этим лимитером не ограничены.
  Осознанно не мигрировано на per-circuit/Hub-level лимитер (например, через `IHubFilter`):
  сайт закрытый (только приглашённые игроки, самостоятельной регистрации нет), угроза
  анонимного flood-а через уже открытый circuit несопоставима по цене риска с усложнением кода —
  пересмотреть, если состав пользователей изменится (публичная регистрация, много незнакомых
  друг другу игроков и т.п.).
- Домен `relaxerr-dnd-economy.ru` куплен на Aeza, DNS не настроен.
- **Auth без self-регистрации**: `AddIdentityCore` + явный `AddAuthentication().AddIdentityCookies()`
  (не полный `AddIdentity`) + `AddAuthorizationCore()` с secure-by-default `FallbackPolicy`
  (`RequireAuthenticatedUser` + assertion по claim `MustChangePassword`). Экран "пригласить
  пользователя" — это Фаза 3; до него первый админ и тестовые игроки заводятся сидингом
  (`IdentitySeeder`, секция `AdminSeed` в конфиге/user-secrets/переменных окружения
  `AdminSeed__Email`/`AdminSeed__Password` на сервере). **Правило проекта: `UserName` всегда
  равен `Email`** — `PasswordSignInAsync` ищет по `UserName`, не по `Email`.
- Login/Logout/смена пароля — статичные SSR Blazor-страницы (`Components/Account/Pages/`,
  атрибут `[ExcludeFromInteractiveRouting]` на всю папку через `_Imports.razor`): `SignInManager`
  пишет HTTP-заголовки, что невозможно из уже начавшего рендериться `InteractiveServer`-
  компонента. Остальное приложение — `InteractiveServer` глобально, страницы `Account/*`
  выпадают из этого через атрибут.
- Поиск в каталоге — Postgres `pg_trgm`, но **word_similarity с порогом 0.4, заданным в коде**
  (`CatalogReadStore.WordSimilarityThreshold`), а не через GUC `pg_trgm.word_similarity_threshold`
  (умалчиваемые 0.6 не проходят частые опечатки типа "кольчюга"→"Кольчуга" = 0.556; GUC —
  настройка уровня БД, легко забыть на сервере). Из-за этого GIN-индекс `gin_trgm_ops` сейчас
  не участвует в поиске (сравнение через функцию, не оператор `%`/`<%`) — не проблема при
  масштабе каталога одной кампании.
- **Кириллица и локаль Postgres**: колонка `Item.NameRu` — `COLLATE "ru-x-icu"` (ICU, входит
  в поставку Postgres, не зависит от локали ОС). Без этого сортировка "по алфавиту" на сервере
  с локалью по умолчанию (например `en_US`) даёт визуально случайный порядок кириллицы — сама
  БД для локальной разработки создавалась с `en_US.UTF-8` (Homebrew `postgresql@16` default),
  и без явной коллации в схеме `ORDER BY "NameRu"` был бы сломан на любом сервере с не-русской
  локалью. Проверить при деплое, что целевой Postgres собран с ICU (`SELECT collname FROM
  pg_collation WHERE collname = 'ru-x-icu';` — должна вернуться строка).
- **Toast-уведомления об успешных действиях**: `Services/ToastService.cs` (Scoped, простое
  событие `OnShow`) + `Components/Layout/ToastContainer.razor` (подписывается, автоскрытие через
  `Task.Delay(4000)`), справа внизу. Используется только на уже интерактивных страницах — сам
  компонент требует circuit, добавлять его на статичные `EditForm`-страницы бессмысленно (там уже
  есть инлайн-баннер `status-message--success` после SSR-постбэка).

## Экономическая логика (перенесена из Excel один в один)

```
РассчитаннаяСтоимость = БазоваяСтоимость × КэфСессии × КэфГорода × КэфСезона
СтоимостьПокупки      = null ("Нет в наличии"), если РассчитаннаяСтоимость ≤ 0
СтоимостьПродажи      = РассчитаннаяСтоимость × КэфПродажи, либо
                         БазоваяСтоимость × (1 + (1 − КэфПродажи)), если товара нет в наличии
```
Реализация — `DndEconomy.Application/Pricing/PriceCalculationService.cs`.
"Партия" в исходнике = `EconomySession` в коде (город/сезон/дата/коэффициенты на момент времени).

## Известные проблемы / TODO

- **Статичные SSR-формы теряли данные (исправлено 2026-08-09, два разных бага сразу)**:
  1. На каждой странице `MainLayout` рендерит форму логаута без `@formname` — как только на той же
     странице появлялась ещё одна форма без имени (`EditForm` без `FormName`), Blazor не мог понять,
     какую форму отправили: `An exception ... does not specify which form is being submitted`.
     Пофикшено — у формы логаута `@formname="logout"`, у всех `EditForm` на статичных SSR-страницах
     свой `FormName` (`item-request`, `new-item`, `invite-user`, `new-city`, `new-session`).
     `AdminEconomy` — единственная страница с двумя формами сразу, там имена обязаны различаться.
  2. Отдельная, более коварная проблема: даже после того как форму стало можно отличить, значения
     всё равно не долетали до модели — валидация ВСЕГДА требовала обязательные поля, даже если они
     были заполнены. Причина: `newRequest`/`model`/`newCity`/`newSession`/`newUser` были обычными
     `private`-полями, а не `[SupplyParameterFromForm]`-свойствами. Для статичного SSR (без circuit)
     это единственный способ, которым POST-данные попадают обратно в модель компонента — обычное
     поле с `@bind-Value` работает только при живой интерактивности (circuit), а не через
     страница-туда-обратно постбэк. Паттерн подсмотрен в `Login.razor`/`ForceChangePassword.razor`
     (единственных местах, где это было сделано правильно с самого начала): свойство
     `[SupplyParameterFromForm] private T Model { get; set; } = default!;` + `Model ??= new();` в
     `OnInitialized(Async)`. На странице с двумя формами (`AdminEconomy`) у атрибута нужен явный
     `FormName`, совпадающий с `EditForm`'овским (`[SupplyParameterFromForm(FormName = "new-city")]`).
     Страницы с `@rendermode InteractiveServer` (`AdminRequests`) этой проблеме не подвержены —
     там `@bind-Value` работает напрямую через живой circuit, `SupplyParameterFromForm` не нужен.
- **Ни одна страница не была интерактивной (исправлено 2026-08-09)** — `Program.cs` регистрировал
  `AddInteractiveServerRenderMode()`, но НИ ОДИН компонент фактически не объявлял
  `@rendermode InteractiveServer` (ни глобально на `<Routes>` в `App.razor`, ни на страницах) —
  весь сайт молча рендерился в чистом статичном SSR. Из-за этого не работало вообще ничего, что
  не является настоящим HTTP form-post: избранное (`@onclick` на wax-seal), пагинация/поиск в
  каталоге (`@oninput`/`@onclick`), "прочитано" у уведомлений, одобрить/отклонить в заявках,
  и загрузка файла в `<InputFile>` на импорте Excel — эти компоненты рендерились, но клики/выбор
  файла не производили НИКАКОГО эффекта (не было ни ошибки, ни исключения — событие просто некому
  было принять без circuit'а). **Важно**: `@rendermode="InteractiveServer"` на `<Routes>`
  ЛОМАЕТ `[ExcludeFromInteractiveRouting]`-страницы (Login и т.д. начинают рендерить "Страница не
  найдена" при прямом заходе, проверено эмпирически) — правильный фикс здесь: `@rendermode
  InteractiveServer` ТОЧЕЧНО на каждой странице, которой реально нужна интерактивность
  (`CatalogIndex`, `CatalogItemDetail`, `NotificationsIndex`, `AdminRequests`, `AdminImport`), а
  не глобально. Страницы на чистом `EditForm`+`OnValidSubmit` (создание предмета, приглашение
  пользователя, города/сессии) в интерактивности не нуждаются — POST и так работает в SSR.
- **Импорт Excel молча не сохранял новые предметы (исправлено 2026-08-09)** —
  `ExcelEconomyImportService.ImportItemsAsync` проверял `if (item.Id == default)`, чтобы решить,
  новый предмет или существующий, но `AuditableEntity.Id` инициализируется `Guid.NewGuid()` уже
  в конструкторе — проверка никогда не была истинной для новых предметов, `dbContext.Items.Add`
  не вызывался, а `summary.ItemsImported++` всё равно врал об успехе. Фикс: отслеживать "новый ли
  объект" через `TryGetValue` по словарю существующих записей, а не по состоянию `Id` (тот же
  паттерн уже случайно работал для `EconomySession` из-за резервного `||`-условия — тоже приведено
  к явному виду). Также добавлено копирование `InputFile`-потока в `MemoryStream` перед
  `XLWorkbook(stream)` — `BrowserFileStream` не поддерживает `Seek`, а xlsx читается как ZIP.
- **DbContext concurrency в Blazor Server** (`System.InvalidOperationException: A second
  operation was started on this context instance...`) — Scoped-инжект `ApplicationDbContext`
  в Blazor Server означает ОДИН экземпляр на весь circuit (SignalR-соединение), а не на
  HTTP-запрос, и любые два сервиса, дёрнувшиеся к БД одновременно на одной странице (например,
  счётчик уведомлений в `MainLayout`, вызываемый на КАЖДОЙ странице, параллельно с запросом самой
  страницы), конкурировали за общий контекст. **Исправлено полностью**: все Infrastructure-сервисы
  (`NotificationService`, `CatalogReadStore`, `EconomyAdminService`, `EconomyPricingReadStore`,
  `ItemAdminService`, `ItemRequestService`, `PlayerProfileService`, `AdminUserService`,
  `ExcelEconomyImportService`) теперь берут собственный короткоживущий контекст через
  `IDbContextFactory<ApplicationDbContext>` вместо общего на circuit — общий Scoped-инжект
  `ApplicationDbContext` в коде проекта больше нигде не используется (только `AddDbContextFactory`
  в `DependencyInjection.cs`, который регистрирует и то, и другое). Identity (`UserManager`/
  `SignInManager`/`RoleManager`) по-прежнему использует общий Scoped-контекст через
  `AddEntityFrameworkStores` — это библиотечный код ASP.NET Core Identity, отдельная история, если
  когда-нибудь проявится гонка именно там.

## Дорожная карта

| Фаза | Содержание | Статус |
|---|---|---|
| 0 | Каркас, схема БД, импорт Excel | Готово |
| 1 | Auth по приглашениям, каталог, умный поиск, цена | Готово |
| 2 | Профиль игрока (избранное), заявки + уведомления | Готово (без live-пуша — счётчик обновляется при навигации) |
| 3 | Админ-панель: пользователи, сессии/коэффициенты, заявки, создание предмета | Готово (см. "Известные проблемы" — гонка DbContext) |
| 4 | Rate limiting под нагрузкой, деплой, полировка стиля | Деплой готов (Docker + автодеплой из git + HTTPS через Certbot на `relaxerr-dnd-economy.ru`), полировка стиля — не начата |

## Конвенции кода

- Табуляция — 2 пробела (см. `.editorconfig`).
- `///` summary у всех публичных классов и методов.
- `#region` для разделения полей/конструкторов/публичных методов/приватных шагов.
- Умеренное логирование через `ILogger` в сервисах.
- Большие методы — декомпозировать на приватные шаги; большие классы — на отдельные сервисы
  с чёткой ответственностью (не микросервисы-процессы, а границы внутри монолита).

## Визуальный стиль (для Фазы 4)

Apple-минимализм (воздух, крупная типографика, стеклянные карточки) + фэнтезийные акценты
(текстура пергамента, восковая печать вместо иконки избранного). Места под изображения —
с промптами для Midjourney в стиле `--ar 16:9 --v 6`.
