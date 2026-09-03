FROM node:22-alpine AS base
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
RUN corepack enable

FROM base AS builder
WORKDIR /app
ENV CI=true
COPY pnpm-workspace.yaml pnpm-lock.yaml package.json turbo.json ./
COPY common/ ./common/
COPY apps/frontend/admin-web/ ./apps/frontend/admin-web/
COPY infrastructure/nginx/admin-web.conf ./infrastructure/nginx/admin-web.conf
RUN pnpm install --frozen-lockfile
ARG VITE_ADMIN_API_BASE_URL
ARG VITE_ADMIN_APP_NAME
ENV VITE_ADMIN_API_BASE_URL=$VITE_ADMIN_API_BASE_URL
ENV VITE_ADMIN_APP_NAME=$VITE_ADMIN_APP_NAME
RUN pnpm --filter "admin-web..." build

FROM nginx:alpine AS runner
COPY --from=builder /app/apps/frontend/admin-web/dist /usr/share/nginx/html
COPY --from=builder /app/infrastructure/nginx/admin-web.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
