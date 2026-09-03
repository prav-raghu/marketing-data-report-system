---
name: frontend-nextjs
description: Use for customer-facing web applications using Next.js — mandatory for any public-facing or SEO-optimised frontend. Covers App Router structure, SSR, SEO, cookie consent, Zod validation, Axios client setup, Tailwind v4, and React Query. Never use React.js/Vite SPA for customer-facing apps — that's frontend-react, reserved for admin/internal tools.
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

Customer-facing apps always use Next.js with SSR and SEO optimisations — non-negotiable. Admin dashboards and internal tools use React.js/Vite instead (see frontend-react).

## Directory structure

```
apps/frontend/customer-web/
├── app/
│   ├── about/
│   ├── assets/
│   ├── components/
│   ├── constants/
│   ├── hooks/
│   ├── services/
│   ├── store/
│   ├── types/
│   ├── utils/
│   ├── globals.css
│   ├── layout.tsx
│   ├── not-found.tsx
│   └── page.tsx
├── public/
├── .env / .env.example
├── next.config.mjs
├── postcss.config.js
├── tailwind.config.ts
└── tsconfig.json
```

Do not deviate from this structure.

## Toolchain

Next.js (latest stable) with TypeScript, Tailwind CSS v4 with PostCSS, React Query for all API-driven state, Axios via centralized client, Zod for form validation, Zustand or Redux when centralized state is needed.

## SEO requirements

Every page must export metadata:
```typescript
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Page Title | Brand Name',
  description: 'Descriptive page summary for search engines',
  openGraph: { title: 'Page Title', description: 'OG description', images: [{ url: '/og-image.png' }] },
};
```

Dynamic routes use `generateMetadata`. Implement `sitemap.ts` and `robots.ts` at the app root. All images use `next/image` with proper `alt` text. Semantic HTML throughout (`<main>`, `<article>`, `<section>`, `<nav>`).

## Naming conventions

| Element | Convention | Example |
|---|---|---|
| Components | `PascalCase` | `HeroSection.tsx` |
| Hooks | `camelCase`, `use` prefix | `useAuth.ts` |
| Constants | `UPPER_SNAKE_CASE` | `API_BASE_URL` |

## Axios API client

Centralized in `app/services/apiClient.ts`, never called directly inside components.

```typescript
const apiClient = axios.create({ baseURL: process.env.NEXT_PUBLIC_CUSTOMER_API_BASE_URL, timeout: 10000 });
```

## React Query — mandatory for API state

```typescript
'use client';
export function useProducts() {
  return useQuery({
    queryKey: ['products'],
    queryFn: () => apiClient.get('/api/products').then(res => res.data),
    retry: 3,
    staleTime: 1000 * 60 * 5,
  });
}
```

Every API-driven component needs a loading skeleton, error state with retry, and empty state.

## Zod validation

Never native browser validation, never `alert()`/`confirm()`.

## Authentication

Access tokens in memory or secure storage; refresh tokens in httpOnly cookies — never `localStorage`.

## Cookie consent

Mandatory on every customer-facing app.

## Tailwind CSS v4 setup

Same conventions as the React SPA app: `postcss.config.js` with `@tailwindcss/postcss`, `tailwind.config.ts` mapping CSS-variable color tokens, `globals.css` with `@import "tailwindcss"`, `:root` variables in `hsl()` format, `@theme` directive, `@layer base` for global border/typography defaults. Theme support via `data-theme` on `<html>` for light/dark/system.

## UI rules

No native `alert()`/`confirm()`/HTML validation. No pill-shaped UI on landing pages unless instructed. Fully mobile responsive. Stock images from Pixabay or Pexels only. No placeholder components. Use `next/image` for all images, `next/link` for internal navigation.

## Contact forms

If no backend endpoint exists, use Formspree or formsubmit.co with full validation.
