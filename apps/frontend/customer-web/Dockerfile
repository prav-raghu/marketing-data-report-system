FROM node:22-alpine AS base
ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
RUN corepack enable

FROM base AS builder
WORKDIR /app
ENV CI=true
COPY pnpm-workspace.yaml pnpm-lock.yaml package.json turbo.json ./
COPY common/ ./common/
COPY apps/frontend/customer-web/ ./apps/frontend/customer-web/
RUN pnpm install --frozen-lockfile
ARG NEXT_PUBLIC_CUSTOMER_API_BASE_URL
ARG NEXT_PUBLIC_CUSTOMER_APP_NAME
ENV NEXT_PUBLIC_CUSTOMER_API_BASE_URL=$NEXT_PUBLIC_CUSTOMER_API_BASE_URL
ENV NEXT_PUBLIC_CUSTOMER_APP_NAME=$NEXT_PUBLIC_CUSTOMER_APP_NAME
RUN pnpm --filter "customer-web..." build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
COPY --from=builder /app/apps/frontend/customer-web/.next/standalone ./
COPY --from=builder /app/apps/frontend/customer-web/.next/static ./apps/frontend/customer-web/.next/static
COPY --from=builder /app/apps/frontend/customer-web/public ./apps/frontend/customer-web/public
EXPOSE 3000
CMD ["node", "apps/frontend/customer-web/server.js"]
