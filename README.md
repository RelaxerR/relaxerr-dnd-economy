# Relaxerr — Экономика ДнД

Веб-сервис экономики для игроков кампании: каталог предметов с динамической ценой
(город/сезон/игровая сессия), заявки на добавление предметов, профиль игрока, админ-панель.

Статус: **Фаза 1 — auth по приглашениям, каталог, умный поиск, цена**. Профиль игрока,
заявки и админ-панель добавляются в следующих фазах.

## Структура решения

```
src/
  DndEconomy.Domain          — сущности и enum'ы, без внешних зависимостей
  DndEconomy.Application     — бизнес-логика (расчёт цены, импорт), не знает про EF Core
  DndEconomy.Infrastructure  — EF Core, PostgreSQL, Identity, импорт Excel
  DndEconomy.Web             — Blazor Server, Program.cs, rate limiting
```

Расчёт цены (`PriceCalculationService`) — прямой перенос формул листа
"Текущая стоимость" исходной таблицы:

```
РассчитаннаяСтоимость = БазоваяСтоимость × КэфСессии × КэфГорода × КэфСезона
СтоимостьПродажи      = РассчитаннаяСтоимость × КэфПродажи (либо штрафной откуп, если нет в наличии)
```

## Первый запуск локально

1. Поднять PostgreSQL, например через Docker:
   ```bash
   docker run --name dnd-economy-db -e POSTGRES_USER=dnd_economy \
     -e POSTGRES_PASSWORD=dev_password -e POSTGRES_DB=dnd_economy_dev \
     -p 5432:5432 -d postgres:16
   ```
2. Восстановить пакеты и установить `dotnet-ef`, если ещё не установлен:
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet restore
   ```
3. Применить миграции (в репозитории уже есть три — `InitialCreate`, `AddTrigramSearchIndexes`,
   `AddCyrillicCollationToItemName`):
   ```bash
   dotnet ef database update -p src/DndEconomy.Infrastructure -s src/DndEconomy.Web
   ```
   Целевой Postgres должен быть собран с ICU (проверить: `SELECT collname FROM pg_collation
   WHERE collname = 'ru-x-icu';` должна вернуть строку) — без этого не применится миграция
   с кириллической коллацией для `Item.NameRu`.
4. Задать первого администратора через user-secrets (без этого при старте будет только
   предупреждение в лог — залогиниться будет некем):
   ```bash
   cd src/DndEconomy.Web
   dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
   dotnet user-secrets set "AdminSeed:Password" "ВременныйПароль123!"
   ```
5. Запустить сайт:
   ```bash
   dotnet run --project src/DndEconomy.Web
   ```
   При первом старте с пустой БД в логе появится "Создан первый администратор" — войти
   под этим email/паролем, дальше приложение принудительно попросит сменить пароль
   (`MustChangePassword=true`), после чего откроется каталог (`/catalog`).

## Что дальше (дорожная карта)

| Фаза | Содержание |
|---|---|
| 0 | ✅ Каркас решения, схема БД, импорт Excel-модели |
| 1 | ✅ Auth по приглашениям, каталог, умный поиск, отображение цены |
| 2 | Профиль игрока (избранное), заявки на предметы + уведомления |
| 3 | Админ-панель: пользователи, сессии/коэффициенты, заявки, создание предмета |
| 4 | Rate limiting под нагрузкой, деплой на сервер, полировка стиля |
