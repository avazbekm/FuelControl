# Kontekst — repo ildizi: docker build -f deploy/Dockerfile .

# 1) PWA (Angular) → src/FuelControl.Api/wwwroot (angular.json: outputPath.base = ../FuelControl.Api/wwwroot)
FROM node:24-alpine AS web
WORKDIR /src/src/FuelControl.Web
COPY src/FuelControl.Web/package.json src/FuelControl.Web/package-lock.json ./
RUN npm ci
COPY src/FuelControl.Web/ ./
RUN npm run build

# 2) API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/FuelControl.Contracts/FuelControl.Contracts.csproj src/FuelControl.Contracts/
COPY src/FuelControl.Core/FuelControl.Core.csproj src/FuelControl.Core/
COPY src/FuelControl.Api/FuelControl.Api.csproj src/FuelControl.Api/
RUN dotnet restore src/FuelControl.Api/FuelControl.Api.csproj
COPY src/FuelControl.Contracts/ src/FuelControl.Contracts/
COPY src/FuelControl.Core/ src/FuelControl.Core/
COPY src/FuelControl.Api/ src/FuelControl.Api/
COPY --from=web /src/src/FuelControl.Api/wwwroot/ src/FuelControl.Api/wwwroot/
RUN dotnet publish src/FuelControl.Api/FuelControl.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Tashkent
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "FuelControl.Api.dll"]
