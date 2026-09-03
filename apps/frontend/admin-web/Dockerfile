FROM node:22-alpine AS css-builder
WORKDIR /app
COPY apps/frontend/admin-web/package.json ./
RUN npm install
COPY apps/frontend/admin-web/src/wwwroot/css/input.css ./src/wwwroot/css/input.css
COPY apps/frontend/admin-web/src/Pages ./src/Pages
COPY apps/frontend/admin-web/src/Layout ./src/Layout
COPY apps/frontend/admin-web/src/Components ./src/Components
COPY apps/frontend/admin-web/src/App.razor ./src/App.razor
COPY apps/frontend/admin-web/src/RedirectToLogin.razor ./src/RedirectToLogin.razor
RUN npx @tailwindcss/cli -i ./src/wwwroot/css/input.css -o ./src/wwwroot/css/app.css --minify

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY apps/frontend/admin-web/ ./apps/frontend/admin-web/
COPY --from=css-builder /app/src/wwwroot/css/app.css ./apps/frontend/admin-web/src/wwwroot/css/app.css
RUN dotnet restore apps/frontend/admin-web/src/AdminWeb.csproj
RUN dotnet publish apps/frontend/admin-web/src/AdminWeb.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS runner
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY infrastructure/nginx/admin-web.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
