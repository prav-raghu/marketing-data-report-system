# Customer Web - Next.js Application

A production-ready customer-facing web application built with Next.js 15+, TypeScript, Tailwind CSS, and modern React patterns.

## 🚀 Tech Stack

- **Next.js 15+** - React framework with App Router for SEO and SSR
- **TypeScript** - Type-safe development
- **Tailwind CSS 4** - Utility-first styling with PostCSS
- **Zustand** - Lightweight state management
- **Axios** - HTTP client with interceptors and retry logic
- **React Query** - Server state management
- **React Hook Form** - Form validation with Zod
- **date-fns** - Date manipulation

## 📁 Project Structure

```
customer-web/
├── app/                    # Next.js App Router
│   ├── layout.tsx         # Root layout with metadata
│   ├── page.tsx           # Home page
│   ├── not-found.tsx      # 404 page
│   ├── about/             # About page route
│   ├── components/        # React components
│   ├── hooks/             # Custom React hooks
│   ├── services/          # API client and services
│   ├── store/             # Zustand stores
│   ├── types/             # TypeScript types
│   ├── utils/             # Utility functions
│   └── globals.css        # Global styles
├── public/                # Static assets
├── next.config.mjs        # Next.js configuration
├── tailwind.config.ts     # Tailwind configuration
├── postcss.config.js      # PostCSS configuration
└── tsconfig.json          # TypeScript configuration
```

## 🚀 Getting Started

### Install Dependencies

```bash
npm install
```

### Development Server

```bash
npm run dev
```

The application will be available at `http://localhost:5173`

### Build for Production

```bash
npm run build
```

### Start Production Server

```bash
npm run start
```

### Type Check

```bash
npm run check
```

### Lint

```bash
npm run lint
```

## 🌍 Environment Variables

Create a `.env` file based on `.env.example`:

```bash
# API Configuration
NEXT_PUBLIC_CUSTOMER_API_BASE_URL=/api

# Feature Flags
NEXT_PUBLIC_ENABLE_ANALYTICS=false

# Environment
NEXT_PUBLIC_ENV=development
```

All client-side environment variables must be prefixed with `NEXT_PUBLIC_`.

## 🎨 Styling

This application uses Tailwind CSS v4 with the PostCSS approach:

- CSS variables defined in `app/globals.css`
- Theme support (light/dark) via `data-theme` attribute
- Custom color tokens mapped to Tailwind utilities

## 🧭 Routing

Next.js uses file-based routing in the `app/` directory:

- `app/page.tsx` → `/`
- `app/about/page.tsx` → `/about`
- `app/not-found.tsx` → 404 page

Use the `Link` component from `next/link` for navigation:

```tsx
import Link from 'next/link';

<Link href="/about">About</Link>
```

## 📦 State Management

### Zustand Stores

Global state is managed with Zustand:

```tsx
import { useCounterStore } from '@/store/useCounterStore';

const { count, increment } = useCounterStore();
```

## 🌐 API Integration

All API calls are made through the centralized Axios client in `app/services/apiClient.ts`:

- Automatic retry logic
- Request/response interceptors
- Bearer token authentication
- Error normalization

The Next.js proxy rewrites `/api/*` to `http://localhost:3000/*` during development.

## 🎯 SEO & Metadata

Next.js provides built-in metadata handling:

### Page-level Metadata

```tsx
export const metadata: Metadata = {
  title: 'Page Title',
  description: 'Page description',
};
```

### Dynamic Metadata

```tsx
export async function generateMetadata(): Promise<Metadata> {
  return {
    title: 'Dynamic Title',
  };
}
```

## 🔒 Security

- CSRF protection via SameSite cookies
- XSS protection with React's JSX escaping
- Content Security Policy headers (configured in next.config.mjs)
- Secure authentication token handling

## 🚢 Deployment

### Vercel (Recommended)

```bash
vercel
```

### Docker

```bash
docker build -t customer-web .
docker run -p 3000:3000 customer-web
```

### Static Export (Optional)

For static hosting, update `next.config.mjs`:

```js
const nextConfig = {
  output: 'export',
};
```

Then build:

```bash
npm run build
```

## 📝 Migration Notes

This application was migrated from React + Vite to Next.js 15+ for improved SEO and server-side rendering capabilities. Key changes:

- React Router → Next.js App Router
- Vite → Next.js dev server
- `import.meta.env.VITE_*` → `process.env.NEXT_PUBLIC_*`
- Client-side only → Hybrid SSR/CSR

## 🤝 Contributing

Follow the monorepo conventions:

- Use `@/` imports for app-relative paths
- Add `"use client"` to components using hooks or browser APIs
- Server components by default (no directive needed)
- Follow TypeScript strict mode rules
- Never use `any` type

## 📚 Resources

- [Next.js Documentation](https://nextjs.org/docs)
- [Next.js App Router](https://nextjs.org/docs/app)
- [Tailwind CSS](https://tailwindcss.com)
- [Zustand](https://zustand-demo.pmnd.rs)

