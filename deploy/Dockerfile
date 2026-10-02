# Kontekst — repo ildizi: docker build -f deploy/Dockerfile .

# 1) PWA (Angular) → src/backend/FuelControl.Api/wwwroot (angular.json: outputPath.base = ../../../backend/FuelControl.Api/wwwroot)
FROM node:24-alpine AS web
WORKDIR /src/src/frontend/web/FuelControl.Web
COPY src/frontend/web/FuelControl.Web/package.json src/frontend/web/FuelControl.Web/package-lock.json ./
RUN npm ci
COPY src/frontend/web/FuelControl.Web/ ./
RUN npm run build

# 2) API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/backend/FuelControl.Contracts/FuelControl.Contracts.csproj src/backend/FuelControl.Contracts/
COPY src/backend/FuelControl.Core/FuelControl.Core.csproj src/backend/FuelControl.Core/
COPY src/backend/FuelControl.Api/FuelControl.Api.csproj src/backend/FuelControl.Api/
RUN dotnet restore src/backend/FuelControl.Api/FuelControl.Api.csproj
COPY src/backend/FuelControl.Contracts/ src/backend/FuelControl.Contracts/
COPY src/backend/FuelControl.Core/ src/backend/FuelControl.Core/
COPY src/backend/FuelControl.Api/ src/backend/FuelControl.Api/
COPY --from=web /src/src/backend/FuelControl.Api/wwwroot/ src/backend/FuelControl.Api/wwwroot/
RUN dotnet publish src/backend/FuelControl.Api/FuelControl.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Tashkent
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "FuelControl.Api.dll"]
