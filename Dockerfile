# Stage 1: Compilar Frontend Vue 3 + TypeScript
FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts
COPY frontend/ ./
RUN npm run build

# Stage 2: Compilar y publicar Backend .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY CustomPresenter.sln Directory.Build.props ./
COPY src/CustomPresenter.Domain/CustomPresenter.Domain.csproj src/CustomPresenter.Domain/
COPY src/CustomPresenter.Application/CustomPresenter.Application.csproj src/CustomPresenter.Application/
COPY src/CustomPresenter.Infrastructure/CustomPresenter.Infrastructure.csproj src/CustomPresenter.Infrastructure/
COPY src/CustomPresenter.Desktop/CustomPresenter.Desktop.csproj src/CustomPresenter.Desktop/
RUN dotnet restore src/CustomPresenter.Desktop/CustomPresenter.Desktop.csproj
COPY src/ src/
RUN dotnet publish src/CustomPresenter.Desktop/CustomPresenter.Desktop.csproj -c Release --no-restore -o /app/publish

# Stage 3: Runtime mínimo no-root
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system appuser && useradd --system --gid appuser --create-home appuser

COPY --from=dotnet-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

RUN mkdir -p /app/data && chown -R appuser:appuser /app /app/data

USER appuser

ENV CUSTOM_PRESENTER_CONTAINER=true \
    CUSTOM_PRESENTER_DATA_DIR=/app/data \
    CUSTOM_PRESENTER_PORT=47321

EXPOSE 47321
VOLUME ["/app/data"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD ["dotnet", "/app/CustomPresenter.Desktop.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "/app/CustomPresenter.Desktop.dll"]
