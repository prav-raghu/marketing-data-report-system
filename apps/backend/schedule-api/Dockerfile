FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY common/ ./common/
COPY apps/backend/schedule-api/ ./apps/backend/schedule-api/
RUN dotnet restore apps/backend/schedule-api/src/ScheduleApi.csproj
RUN dotnet publish apps/backend/schedule-api/src/ScheduleApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 4003
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:4003/api/v1/ping || exit 1
ENTRYPOINT ["dotnet", "ScheduleApi.dll"]
