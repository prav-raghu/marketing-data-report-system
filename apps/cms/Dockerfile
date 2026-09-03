FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY common/ ./common/
COPY apps/cms/ ./apps/cms/
RUN dotnet restore apps/cms/src/Cms.csproj
RUN dotnet publish apps/cms/src/Cms.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 4005
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://127.0.0.1:4005/manager/login || exit 1
ENTRYPOINT ["dotnet", "Cms.dll"]
