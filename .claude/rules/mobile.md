---
paths:
  - "apps/mobile/**/*.ts"
  - "apps/mobile/**/*.tsx"
---

# Mobile Rules

You are working on the Ionic React + Capacitor mobile app under `apps/mobile/`.

## Environment variables

This app runs on Vite, so it follows the same `VITE_<SCOPE>_*` convention as `admin-web` (see `rules/frontend.md`) — scope is `MOBILE`: `VITE_MOBILE_API_BASE_URL`. Never `VITE_API_BASE_URL` unscoped — it collides with other Vite apps in a shared env/CI variable list.

## Non-negotiable

- ALL persistent storage: `@capacitor/preferences` — never `localStorage` or `sessionStorage`
- ALL API calls: through `src/services/apiClient.ts` — never raw fetch/axios in components
- ALL data fetching: React Query — never `useEffect` + axios
- ALL page roots: `IonPage` with `IonHeader` + `IonContent` — never bare `div`
- ALL list pages: `IonInfiniteScroll` + `useInfiniteQuery` — never pagination buttons
- ALL data pages: pull-to-refresh with `IonRefresher` + skeleton loading + error + empty states
- No `any` type, no comments in code

## Platform guard (required before every native plugin call)

```typescript
import { Capacitor } from '@capacitor/core';
if (Capacitor.isNativePlatform()) {
  // native plugin call
}
```

## Required React Query config for mobile

```typescript
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      networkMode: 'offlineFirst',
      staleTime: 1000 * 60 * 10,
      retry: 2,
      gcTime: 1000 * 60 * 60 * 24,
    },
  },
});
```

## Naming

Same as React frontend:
- Pages: `PascalCase.tsx` in `src/pages/`
- Components: `PascalCase.tsx` in `src/components/{feature}/`
- Hooks: `use{Name}.ts` in `src/hooks/`
- Services: `camelCase.ts` in `src/services/`

## Before Native Build Checklist

- [ ] `appId` in `capacitor.config.ts` updated to production value
- [ ] `appName` updated
- [ ] `VITE_API_BASE_URL` pointing to accessible server IP, not `localhost`
- [ ] `pnpm build` passes
- [ ] `npx cap sync` run after build
- [ ] Push notification permissions set in native project settings
- [ ] Signing certificates configured in Xcode / Android Studio

## Before marking complete

Run `pnpm --filter customer-mobile typecheck` — zero errors required.
