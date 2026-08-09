FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DndEconomy.sln Directory.Build.props ./
COPY src/DndEconomy.Domain/DndEconomy.Domain.csproj src/DndEconomy.Domain/
COPY src/DndEconomy.Application/DndEconomy.Application.csproj src/DndEconomy.Application/
COPY src/DndEconomy.Infrastructure/DndEconomy.Infrastructure.csproj src/DndEconomy.Infrastructure/
COPY src/DndEconomy.Web/DndEconomy.Web.csproj src/DndEconomy.Web/
RUN dotnet restore src/DndEconomy.Web/DndEconomy.Web.csproj

COPY src/ src/
RUN dotnet publish src/DndEconomy.Web/DndEconomy.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "DndEconomy.Web.dll"]
