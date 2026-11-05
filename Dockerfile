FROM node:24-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS backend
WORKDIR /src
COPY global.json ./
COPY backend/PennyPath.Api/PennyPath.Api.csproj backend/PennyPath.Api/
RUN dotnet restore backend/PennyPath.Api/PennyPath.Api.csproj
COPY backend/PennyPath.Api/ backend/PennyPath.Api/
COPY --from=frontend /src/frontend/dist/ backend/PennyPath.Api/wwwroot/
RUN dotnet publish backend/PennyPath.Api/PennyPath.Api.csproj \
    -c Release --no-restore --self-contained false -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend /out/ ./
LABEL org.opencontainers.image.source="https://github.com/pawelkam/PennyPath"
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "PennyPath.Api.dll"]
