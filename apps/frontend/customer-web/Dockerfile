FROM node:22-alpine AS css-builder
WORKDIR /app
COPY apps/frontend/customer-web/package.json ./
RUN npm install
COPY apps/frontend/customer-web/src/wwwroot/css/input.css ./src/wwwroot/css/input.css
COPY apps/frontend/customer-web/src/Pages ./src/Pages
COPY apps/frontend/customer-web/src/Layout ./src/Layout
COPY apps/frontend/customer-web/src/App.razor ./src/App.razor
COPY apps/frontend/customer-web/src/Routes.razor ./src/Routes.razor
COPY apps/frontend/customer-web/src/Client/Pages ./src/Client/Pages
RUN npx @tailwindcss/cli -i ./src/wwwroot/css/input.css -o ./src/wwwroot/css/app.css --minify

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY apps/frontend/customer-web/ ./apps/frontend/customer-web/
COPY --from=css-builder /app/src/wwwroot/css/app.css ./apps/frontend/customer-web/src/wwwroot/css/app.css
RUN dotnet restore apps/frontend/customer-web/src/CustomerWeb.csproj
RUN dotnet publish apps/frontend/customer-web/src/CustomerWeb.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_URLS=http://0.0.0.0:3000
EXPOSE 3000
ENTRYPOINT ["dotnet", "CustomerWeb.dll"]
