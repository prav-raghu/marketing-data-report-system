---
name: frontend-react
description: Use for React.js single-page applications such as admin dashboards and internal tools. Covers component creation, hooks, Axios API clients, React Query, Zustand/Redux state, React Router, Tailwind v4 styling, Zod validation, and admin dashboard layouts. Do NOT use for customer-facing apps — those use Next.js (frontend-nextjs).
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

React.js apps are for non-SEO use cases: admin dashboards, internal tools, back-office portals. Customer-facing apps always use Next.js instead.

## Directory structure

```
apps/frontend/[app-name]/
├── src/
│   ├── components/
│   │   ├── auth/
│   │   └── shared/
│   ├── pages/
│   ├── hooks/
│   ├── services/
│   │   └── apiClient.ts
│   ├── store/
│   ├── utils/
│   ├── types/
│   ├── constants/
│   └── App.tsx
├── public/
├── package.json
├── tsconfig.json
└── vite.config.ts
```

Do not deviate from this structure.

## Toolchain

Vite + React + TypeScript, Tailwind CSS v4 with PostCSS, React Router, React Query for all API-driven state, Zustand or Redux for centralized state when needed, Axios for all API calls via a centralized client, Zod for client-side form validation.

## Naming conventions

| Element | Convention | Example |
|---|---|---|
| Components | `PascalCase` | `UserProfile.tsx` |
| Hooks | `camelCase`, `use` prefix | `useAuth.ts` |
| Utilities | `camelCase` | `formatDate.ts` |
| Constants | `UPPER_SNAKE_CASE` | `API_BASE_URL` |

## Axios API client

All calls go through `src/services/apiClient.ts` — never direct Axios calls inside components or `useEffect`.

```typescript
import axios from 'axios';

const apiClient = axios.create({ baseURL: import.meta.env.VITE_ADMIN_API_BASE_URL, timeout: 10000 });

apiClient.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => Promise.reject(normalizeError(error))
);

export default apiClient;
```

## Vite proxy (dev)

```typescript
server: {
  proxy: {
    '/api': { target: 'http://localhost:4000', changeOrigin: true, rewrite: (path) => path.replace(/^\/api/, '') },
  },
}
```

All API calls use `/api/...` — never reference internal service ports directly.

## React Query — mandatory for API state

```typescript
export function useUsers() {
  return useQuery({
    queryKey: ['users'],
    queryFn: () => apiClient.get('/api/users').then(res => res.data),
    retry: 3,
    staleTime: 1000 * 60 * 5,
  });
}
```

Every API-driven component must have a loading skeleton, error state with retry, and empty state.

## Zod validation

```typescript
const createUserSchema = z.object({
  email: z.string().email(),
  password: z.string().min(8).max(128),
  name: z.string().min(2).max(100),
});
```

Never native browser form validation, never `alert()`/`confirm()`.

## Admin dashboard layout

Left sidenav (persistent), top navigation bar, main content area.

## Authentication

Access tokens in memory or secure storage wrappers; refresh tokens in httpOnly cookies — never `localStorage`.

## Tailwind CSS v4 setup

`postcss.config.js` must use `@tailwindcss/postcss` (never the `@tailwindcss/vite` plugin alone). `tailwind.config.ts` colors map to CSS variables (`background`, `foreground`, `muted`, `popover`, `card`, `border`, `input`, `primary`, `secondary`, `accent`, `destructive`, `ring`). `index.css` uses `@import "tailwindcss"` (v4 single import, not v3's `@tailwind base`), defines `:root` variables in `hsl()` format, includes the `@theme` directive mapping variables to tokens, and a `@layer base` block applying `border-border` globally and base typography.

Theme support via `data-theme` on `<html>` for light, dark, and system.

## UI rules

No native `alert()`, `confirm()`, or HTML validation popups. No pill-shaped UI elements on landing pages unless explicitly instructed. Fully mobile responsive. Stock images from Pixabay or Pexels only. No placeholder/sample components — production-ready only.

## Contact forms

If no backend endpoint exists, use Formspree or formsubmit.co, with proper validation and error handling.
