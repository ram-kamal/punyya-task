# Multi-stage Dockerfile for PHF.Api (.NET 8 Web API + Next.js Admin Center)

# Stage 1: Build Next.js Web Admin Portal
FROM node:20-alpine AS web-build
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# Stage 2: Build .NET 8 Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for caching dependency restore
COPY ["backend/src/PHF.Core/PHF.Core.csproj", "backend/src/PHF.Core/"]
COPY ["backend/src/PHF.Infrastructure/PHF.Infrastructure.csproj", "backend/src/PHF.Infrastructure/"]
COPY ["backend/src/PHF.Realtime/PHF.Realtime.csproj", "backend/src/PHF.Realtime/"]
COPY ["backend/src/PHF.Api/PHF.Api.csproj", "backend/src/PHF.Api/"]

RUN dotnet restore "backend/src/PHF.Api/PHF.Api.csproj"

# Copy all source files and publish
COPY backend/ backend/
WORKDIR "/src/backend/src/PHF.Api"
RUN dotnet publish "PHF.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Copy static web admin build into publish/wwwroot
COPY --from=web-build /web/out /app/publish/wwwroot

# Stage 3: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Render passes PORT via environment variable (default 10000 or 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PHF.Api.dll"]

