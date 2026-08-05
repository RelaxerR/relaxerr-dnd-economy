# Relaxerr — Экономика ДнД

Веб-сервис экономики для игроков кампании: каталог предметов с динамической ценой
(город/сезон/игровая сессия), заявки на добавление предметов, профиль игрока, админ-панель.

Статус: **Фаза 0 — каркас решения и схема БД**. UI, поиск, профиль и админка добавляются
в следующих фазах.

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
3. Создать первую миграцию и применить её:
   ```bash
   dotnet ef migrations add InitialCreate -p src/DndEconomy.Infrastructure -s src/DndEconomy.Web
   dotnet ef database update -p src/DndEconomy.Infrastructure -s src/DndEconomy.Web
   ```
4. Запустить сайт:
   ```bash
   dotnet run --project src/DndEconomy.Web
   ```

Код написан офлайн, без доступа к сети и SDK — после `dotnet restore`/`dotnet build`
возможны точечные правки версий пакетов или мелкие синтаксические расхождения.

## Что дальше (дорожная карта)

| Фаза | Содержание |
|---|---|
| 0 | ✅ Каркас решения, схема БД, импорт Excel-модели |
| 1 | Auth по приглашениям, каталог, умный поиск, отображение цены |
| 2 | Профиль игрока (избранное), заявки на предметы + уведомления |
| 3 | Админ-панель: пользователи, сессии/коэффициенты, заявки, создание предмета |
| 4 | Rate limiting под нагрузкой, деплой на сервер, полировка стиля |
