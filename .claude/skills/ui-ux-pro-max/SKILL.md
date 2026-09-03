---
description: UI/UX design intelligence for building professional frontend interfaces. Covers styles, color palettes, font pairings, UX guidelines, and component patterns across React, Next.js, and Ionic. Invoke when designing new pages, creating or refactoring UI components, choosing visual styles, or when UI looks unprofessional and the reason is unclear.
argument-hint: <page or component description, e.g. "admin dashboard with data table and sidebar">
---

# UI/UX Design Intelligence

This skill provides professional design guidance for this monorepo's frontend stack: React + Vite (admin-web), Next.js (customer-web), and Ionic + Capacitor (customer-mobile), all using Tailwind v4.

## How to use

Run a design system search before writing frontend code:

```bash
python3 ~/.claude/skills/ui-ux-pro-max/scripts/search.py "$ARGUMENTS" --design-system --stack react
python3 ~/.claude/skills/ui-ux-pro-max/scripts/search.py "$ARGUMENTS" --design-system --stack nextjs
python3 ~/.claude/skills/ui-ux-pro-max/scripts/search.py "$ARGUMENTS" --design-system --stack react-native
```

If the global skill is not installed, use the design principles in this file directly.

## Installation (run once globally)

```bash
npm install -g ui-ux-pro-max-cli
uipro init --ai claude --global
```

Requires Python 3.x: `python3 --version`

## Stack-specific flags

| App | Flag |
|---|---|
| admin-web (React + Vite) | `--stack react` |
| customer-web (Next.js) | `--stack nextjs` |
| customer-mobile (Ionic) | `--stack react-native` |

## Project design principles

This project uses Tailwind v4 with CSS variable color tokens. All design decisions must use the existing token system — never hardcode hex colors.

### Token system

```css
:root {
  --background: hsl(...);
  --foreground: hsl(...);
  --primary: hsl(...);
  --primary-foreground: hsl(...);
  --secondary: hsl(...);
  --muted: hsl(...);
  --muted-foreground: hsl(...);
  --card: hsl(...);
  --border: hsl(...);
  --destructive: hsl(...);
  --ring: hsl(...);
}
```

Use `bg-background`, `text-foreground`, `bg-primary`, `text-destructive` etc — never raw color classes like `bg-blue-500`.

### Admin-web design rules

- Left sidenav, top navigation bar, main content area with breadcrumbs
- Tables with pagination, search input, filter controls, action buttons
- Cards use `bg-card border border-border rounded-lg`
- Loading states use skeleton via `bg-muted animate-pulse`
- Danger actions use `text-destructive` / `bg-destructive`
- No pill-shaped primary buttons unless explicitly requested

### Customer-web design rules

- Semantic HTML throughout: `<main>`, `<article>`, `<section>`, `<nav>`
- All images via `next/image` with proper `alt` text
- All internal links via `next/link`
- Mobile responsive first
- No placeholder/stock components — production-ready only

### Mobile design rules

- Native feel: `IonCard`, `IonList`, `IonItem` for content lists
- `IonSkeletonText` for loading states
- `IonRefresher` for pull-to-refresh
- Respect safe areas: `ion-padding` for content spacing
- Animations: 150–300ms, transform/opacity only

## UX quality checklist (apply to every page)

- Loading state: skeleton or spinner when data is fetching
- Error state: message + retry button
- Empty state: helpful message + call-to-action
- Every interactive element has a focus ring
- No layout shift when data loads
- Mobile responsive at 375px minimum
- Color contrast minimum 4.5:1 for text
- Tap targets minimum 44×44px on mobile
- Form errors displayed below the field, not in alerts
