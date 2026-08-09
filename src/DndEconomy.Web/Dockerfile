FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore и publish намеренно НЕ разделены на "restore по одним .csproj" + "publish --no-restore":
# при таком разделении MSBuild кэширует неполные данные о статических веб-ассетах (посчитанные
# до появления .razor-файлов) и публикует wwwroot/_framework без blazor.web.js — рендер-режим
# InteractiveServer тогда молча не работает нигде на сайте (страница грузится статикой, но ни один
# @onclick/InputFile не отвечает — "ничего не происходит"). Теряем часть ускорения от layer-кэша
# Docker при изменении только исходников, но верный publish важнее.
COPY DndEconomy.sln Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/DndEconomy.Web/DndEconomy.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "DndEconomy.Web.dll"]
