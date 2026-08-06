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
- Reverse proxy на сервере — ещё не выяснено, что уже стоит перед Foundry VTT (nginx/Traefik/
  Caddy) — уточнить перед деплоем.
- DDoS-защита: встроенный `RateLimiter` (глобальный sliding window + строгий на /login) как
  первый рубеж, Cloudflare перед доменом — как второй, более надёжный.
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

## Экономическая логика (перенесена из Excel один в один)

```
РассчитаннаяСтоимость = БазоваяСтоимость × КэфСессии × КэфГорода × КэфСезона
СтоимостьПокупки      = null ("Нет в наличии"), если РассчитаннаяСтоимость ≤ 0
СтоимостьПродажи      = РассчитаннаяСтоимость × КэфПродажи, либо
                         БазоваяСтоимость × (1 + (1 − КэфПродажи)), если товара нет в наличии
```
Реализация — `DndEconomy.Application/Pricing/PriceCalculationService.cs`.
"Партия" в исходнике = `EconomySession` в коде (город/сезон/дата/коэффициенты на момент времени).

## Дорожная карта

| Фаза | Содержание | Статус |
|---|---|---|
| 0 | Каркас, схема БД, импорт Excel | Готово |
| 1 | Auth по приглашениям, каталог, умный поиск, цена | Готово |
| 2 | Профиль игрока (избранное), заявки + уведомления | Не начато |
| 3 | Админ-панель: пользователи, сессии/коэффициенты, заявки, создание предмета | Не начато |
| 4 | Rate limiting под нагрузкой, деплой, полировка стиля | Не начато |

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
