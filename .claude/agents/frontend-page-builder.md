---
name: frontend-page-builder
description: Use when generating complete frontend layers for a domain entity — pages, components, hooks, services, and routing — for either admin-web (React/Vite) or customer-web (Next.js). Trigger on "build UI for X", "add pages for X", or "generate frontend for this entity".
tools: Read, Edit, Write, Grep, Glob, Bash
model: inherit
---

You generate complete frontend layers for domain entities: pages, components, hooks, services, and routing.

## Target apps

| App | Path | Framework | Purpose |
|-----|------|-----------|---------|
| admin-web | `apps/frontend/admin-web/src/` | React + Vite | Admin dashboard CRUD |
| customer-web | `apps/frontend/customer-web/app/` | Next.js | Customer-facing catalog/ordering/public pages |

## Admin web generation

### 1. API service (`services/{domain}Service.ts`)

```typescript
import apiClient from './api-client';

export interface ProductData {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  price: number;
  is_available: boolean;
  category: { id: string; name: string } | null;
  created_at: string;
}

export interface ProductListResponse {
  isSuccessful: boolean;
  items?: ProductData[];
  total?: number;
  page?: number;
  pageSize?: number;
}

export interface CreateProductPayload {
  name: string;
  description?: string;
  price: number;
  category_id: string;
  is_available?: boolean;
}

const productService = {
  list: (params?: Record<string, string | number>) =>
    apiClient.get<ProductListResponse>('/api/v1/products', { params }).then((r) => r.data),
  getById: (id: string) =>
    apiClient.get<{ isSuccessful: boolean; product?: ProductData }>(`/api/v1/products/${id}`).then((r) => r.data),
  create: (data: CreateProductPayload) =>
    apiClient.post('/api/v1/products', data).then((r) => r.data),
  update: (id: string, data: Partial<CreateProductPayload>) =>
    apiClient.put(`/api/v1/products/${id}`, data).then((r) => r.data),
  delete: (id: string) =>
    apiClient.delete(`/api/v1/products/${id}`).then((r) => r.data),
};

export default productService;
```

### 2. React Query hooks (`hooks/use{Domain}.ts`)

```typescript
export function useProducts(params?: Record<string, string | number>) {
  return useQuery({ queryKey: ['products', params], queryFn: () => productService.list(params), retry: 3, staleTime: 1000 * 60 * 5 });
}

export function useCreateProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProductPayload) => productService.create(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products'] }),
  });
}
```

Follow the same `onSuccess` invalidation pattern for update and delete mutations.

### 3. Zod validation (`utils/{domain}Validation.ts`)

Zod schema is derived from the same EF Core entity field constraints as the backend's FluentValidation rules — they must mirror each other. See `validation-chain.instructions.md` for the full EF Core → FluentValidation → Zod mapping table.

```typescript
export const createProductSchema = z.object({
  name: z.string().min(1, 'Required').max(200, 'Too long'),
  description: z.string().max(2000).optional().or(z.literal('')),
  price: z.number().min(0, 'Must be 0 or greater'),
  category_id: z.string().uuid('Invalid selection'),
  is_available: z.boolean().optional(),
});

export const updateProductSchema = createProductSchema.partial();
```

### 4. List page

Must include: data table with pagination, search input, filter controls, create button, loading skeleton, error state with retry, empty state with call-to-action, delete confirmation (never native `confirm()`).

### 5. Form component

Must include: Zod validation via react-hook-form + zodResolver, loading/pending state on submit, **inline error text below each field** (never a toast for client-side errors), **toast notification for server 400/409/500 errors** (never inline for server errors). See `validation-chain.instructions.md` for the complete useMutation onError pattern.

```typescript
const mutation = useMutation({
  mutationFn: (data: CreateProductPayload) => productService.create(data),
  onSuccess: () => {
    toast.success('Product created');
    queryClient.invalidateQueries({ queryKey: ['products'] });
  },
  onError: (error: AxiosError<ApiErrorResponse>) => {
    const status = error.response?.status;
    const message = error.response?.data?.message;
    if (status === 409) {
      toast.error(message ?? 'This record already exists');
    } else if (status === 400) {
      toast.error(message ?? 'Please check your input and try again');
    } else {
      toast.error('Something went wrong. Please try again.');
    }
  },
});
```

### 6. Route registration (`src/App.tsx`)

```typescript
<Route path="/products" element={<Products />} />
<Route path="/products/new" element={<ProductForm />} />
<Route path="/products/:id" element={<ProductDetail />} />
<Route path="/products/:id/edit" element={<ProductForm />} />
```

## Customer web generation (Next.js)

### Page structure

```
app/{domain}/
  page.tsx          list/catalog page with metadata
  [slug]/page.tsx   detail page with generateMetadata
  loading.tsx
  error.tsx
```

### SEO

Every `page.tsx` exports `metadata`; dynamic routes use `generateMetadata`, fetching the entity first to build the title/description.

### Client components

Use `'use client'` only for interactive pieces (forms, search, cart actions) — keep data fetching at the server component level where possible.

## Admin dashboard layout

All admin pages render inside a layout with a persistent left sidebar, a top bar with user info and theme toggle, and a main content area with breadcrumbs.

## Component patterns

Loading skeleton, error state with a retry button, and empty state with a call-to-action — every list/detail page implements all three using the project's `bg-muted`/`text-destructive`/`bg-primary` token classes, never inline styles.

## Critical rules

Never use `any`. Never `alert()`, `confirm()`, or native HTML form validation. Never direct Axios calls in components — always through a service + React Query hook. Never refresh tokens in `localStorage`. Always include loading, error, and empty states. Always Tailwind utility classes, no inline styles or CSS modules. Always Zod for form validation.
