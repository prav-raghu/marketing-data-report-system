---
paths:
  - "apps/frontend/**/*.ts"
  - "apps/frontend/**/*.tsx"
---

# Frontend Rules

You are working on a frontend application. These rules apply to all files under `apps/frontend/`.

## Stack

- `admin-web`: React + Vite + TypeScript + Tailwind v4
- `customer-web`: Next.js + TypeScript + Tailwind v4

## Environment variable naming convention

Every frontend env var carries an app-scope segment right after the framework prefix, so a shared root `.env`/CI variable list never collides between apps. Format: `<FRAMEWORK_PREFIX>_<SCOPE>_<NAME>`.

| App | Framework prefix | Scope | Example |
|---|---|---|---|
| `admin-web` (Vite) | `VITE_` | `ADMIN` | `VITE_ADMIN_API_BASE_URL` |
| A future employee-facing Vite app | `VITE_` | `EMPLOYEE` | `VITE_EMPLOYEE_API_BASE_URL` |
| `customer-web` (Next.js, browser-exposed) | `NEXT_PUBLIC_` | `CUSTOMER` | `NEXT_PUBLIC_CUSTOMER_API_BASE_URL` |
| A future internal-tooling Next.js app | `NEXT_PUBLIC_` | `TECH` | `NEXT_PUBLIC_TECH_API_BASE_URL` |

**Vite:** the framework only exposes vars to `import.meta.env` that start with `VITE_` (`envPrefix` in `vite.config.ts`) — `VITE_ADMIN_*` satisfies that and adds the scope. Every var an app reads, browser-exposed or not, gets the scope segment: `VITE_ADMIN_API_BASE_URL`, `VITE_ADMIN_APP_NAME`, `VITE_ADMIN_PORT`, `VITE_ADMIN_SENTRY_DSN`.

**Next.js — read this before renaming anything:** the literal prefix `NEXT_PUBLIC_` is a hard Next.js requirement for any var that must reach browser code (Next's build tooling string-replaces only names starting with exactly `NEXT_PUBLIC_` — nothing else is exposed, and you cannot substitute `NEXT_CUSTOMER_` for it). So the scope segment goes **after** the mandatory prefix, never in place of it: `NEXT_PUBLIC_CUSTOMER_API_BASE_URL`, `NEXT_PUBLIC_CUSTOMER_APP_NAME`, `NEXT_PUBLIC_CUSTOMER_SENTRY_DSN`, `NEXT_PUBLIC_CUSTOMER_SITE_URL`. Server-only Next.js env vars (never read in client components, e.g. a server-side API secret) are not required to carry `NEXT_PUBLIC_` at all — scope those as `<SCOPE>_<NAME>` (e.g. `CUSTOMER_INTERNAL_API_KEY`) so they stay out of the client bundle by construction. `PORT` and `NODE_ENV` are framework-reserved and stay unscoped.

Applies retroactively: rename existing `VITE_API_BASE_URL` / `VITE_APP_NAME` → `VITE_ADMIN_*`, and `NEXT_PUBLIC_API_BASE_URL` / `NEXT_PUBLIC_APP_NAME` / `NEXT_PUBLIC_SITE_URL` → `NEXT_PUBLIC_CUSTOMER_*`, everywhere they're declared (`.env.example`, Dockerfile `ARG`/`ENV`, Coolify/GitHub Actions build variables).

## Dates — display as dd/MM/yyyy, optional HH:mm:ss — read date-handling.instructions.md first

Every rendered date/timestamp goes through the shared `formatDate`/`formatDateTime` helpers (`date-fns`, already a dependency) — never `toLocaleDateString()`, never a second date library. Append time (`HH:mm:ss`) only for genuine timestamps (activity/audit views); a plain business date is date-only. Outbound to the API is always ISO 8601, never the display format. See `date-handling.instructions.md` for the full convention and the ready-to-use utility.

## MFA enrollment and login challenge (admin-web only)

Backend pattern (enrollment endpoints, the two-step login flow, TOTP rate-limiting) is documented in `jwt-security.md`'s "MFA / Two-Factor Authentication" section — read that first. This section covers the two `admin-web` screens that talk to it.

### Enrollment page (`/settings/security` or similar, requires an existing session)

1. Call `POST /users/2fa/setup` → response has `{ secret, qrCode }` (`qrCode` is already a `data:image/png;base64,...` URI — render it directly in an `<img>`, no client-side QR generation needed).
2. Show the QR code, plus the raw `secret` as manually-typeable fallback text (some authenticator apps/users prefer typing over scanning).
3. A single 6-digit code input, submitted to `POST /users/2fa/verify` with `{ code }`. Only on success is MFA actually enabled — until this step, `setup2FA` has stored a pending secret but `twoFactorEnabled` is still `false`. Show that distinction in the UI (e.g. "Scan the code, then enter it below to finish enabling 2FA" — don't imply MFA is already on after step 1).
4. Disabling MFA (`POST /users/2fa/disable` with `{ code }`) requires a fresh TOTP code too, the same as enabling — never a plain "toggle off" button. Treat it as a security-sensitive action, same weight as a password change.

### Post-login challenge screen

The login form's `onSuccess` branches on the response shape, not just the HTTP status — both the password-only success and the "need a code" response are `200`:

```typescript
const loginMutation = useMutation({
  mutationFn: (dto: LoginDto) => authService.login(dto),
  onSuccess: (result) => {
    if (result.data.mfaRequired) {
      // stash mfaToken (component state, not persisted storage) and navigate to the challenge screen
      setMfaToken(result.data.mfaToken);
      navigate('/login/verify');
      return;
    }
    // unchanged: single-step login, store authToken, navigate to dashboard
    completeLogin(result.data);
  },
});
```

The challenge screen is a single 6-digit code field submitting `{ mfaToken, code, rememberMe }` to `/auth/verify-login-mfa`. `mfaToken` never touches `localStorage`/`sessionStorage` — it's short-lived (5 minutes) and only useful for this one follow-up call, so component state (or a route param, never the URL query string) is enough; treat it with the same "never persist" rule as the real access token. A wrong code returns `401` with a field-less message — toast it per the standard server-error rule, don't turn it into an inline field error under the code input (it's a server-side verification failure, not a client-side shape failure). Show a countdown or a subtle "code expires in 5 minutes, restart login if it does" — there's no silent refresh for an MFA challenge token; if it expires the user re-enters their password.

## Validation — read validation-chain.instructions.md first

Every form must implement the full validation chain. The two rules that govern error display:

- **Zod client failure** → inline error text below the field. Never a toast.
- **Server 400/409/500** → toast notification via `sonner`. Never inline.

This distinction is non-negotiable. See `validation-chain.instructions.md` for the complete chain, EF Core entity → FluentValidation → Zod mapping, and worked example.

### Required setup per form

react-hook-form + Zod resolver:

```typescript
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
```

Toast library — use `sonner` (already in scope via admin-web and customer-web):

```typescript
import { toast } from 'sonner';

// Success
toast.success('Saved successfully');

// Server error
toast.error(message ?? 'Something went wrong. Please try again.');
```

### Inline error pattern (always the same)

```tsx
<div>
  <input {...register('fieldName')} />
  {errors.fieldName && (
    <span className="text-destructive text-sm mt-1 block">{errors.fieldName.message}</span>
  )}
</div>
```

### useMutation error handler pattern (always the same)

```typescript
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
```

### Zod field types (see full mapping in validation-chain.instructions.md)

| Field | Zod |
|---|---|
| Required string | `z.string().min(1, 'Required')` |
| Email | `z.string().email('Invalid email address')` |
| Phone (SA) | `z.string().regex(/^(\+27\|0)[6-8][0-9]{8}$/, 'Invalid phone number')` |
| Optional field | `.optional().or(z.literal(''))` |
| Enum select | `z.enum(['A', 'B'], { message: 'Please select an option' })` |
| UUID | `z.string().uuid('Invalid selection')` |
| Positive number | `z.number().min(0, 'Must be 0 or greater')` |
| Max length | `.max(N, 'Too long')` |

### Submit button (always)

```tsx
<button type="submit" disabled={isSubmitting || mutation.isPending}>
  {mutation.isPending ? 'Saving...' : 'Save'}
</button>
```

### Checklist before marking a form complete

- [ ] Zod schema covers every required field from the EF Core entity / DTO
- [ ] Every required field has `min(1, 'Required')` or equivalent
- [ ] Email fields have `.email()`
- [ ] Phone fields have `.regex()` with SA pattern
- [ ] Max lengths match the backend FluentValidation `.MaximumLength(N)` / EF Core column size
- [ ] Inline errors display below each field
- [ ] `useMutation` `onError` handles 400, 409, and 500 separately with toast
- [ ] Submit button shows pending state
- [ ] No `alert()`, `confirm()`, or native HTML validation attributes

## Other non-negotiables

- No `any` type
- No comments in code
- No `alert()`, `confirm()`, or native HTML form validation attributes (`required`, `pattern` on `<input>`)
- No `localStorage` for tokens — access tokens in memory, refresh tokens in httpOnly cookie
- No direct Axios calls inside components or `useEffect` — always through a service + React Query hook
- No inline styles — always Tailwind utility classes
- Every API-driven component must have loading skeleton, error state with retry, and empty state

## Tailwind v4 conventions

- `postcss.config.js` with `@tailwindcss/postcss`
- `globals.css` uses `@import "tailwindcss"` (not v3's `@tailwind base`)
- Colors defined as CSS variables in `hsl()` format under `:root`
- `@theme` directive maps CSS vars to Tailwind tokens
- `data-theme` on `<html>` for light/dark/system
- Use semantic token classes: `bg-background`, `text-foreground`, `bg-primary`, `text-destructive` — never raw color classes like `bg-blue-500`

## React Query (mandatory for all API state)

```typescript
export function useItems(params?: Record<string, string | number>) {
  return useQuery({
    queryKey: ['items', params],
    queryFn: () => itemService.list(params),
    retry: 3,
    staleTime: 1000 * 60 * 5,
  });
}
```

## Before marking complete

Run `pnpm --filter <app-name> typecheck` — zero errors required.
