FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY common/ ./common/
COPY apps/backend/customer-api/ ./apps/backend/customer-api/
RUN dotnet restore apps/backend/customer-api/src/CustomerApi.csproj
RUN dotnet publish apps/backend/customer-api/src/CustomerApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 4002
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:4002/api/v1/ping || exit 1
ENTRYPOINT ["dotnet", "CustomerApi.dll"]
