---
name: seo-optimization
description: Audit and improve SEO for Next.js customer-facing apps (App Router) in the monorepo — metadata, structured data, sitemaps, Open Graph, Core Web Vitals, and local-business schema. Use this whenever the user mentions SEO, search rankings, Google indexing, meta tags, meta description, structured data, schema markup, sitemap.xml, robots.txt, Open Graph, canonical URLs, Lighthouse SEO score, or wants a public-facing Next.js app (customer portal, marketing site, landing page) to rank better or be more discoverable. Trigger even if the user just says "SEO for customer-web" or "improve search rankings" without listing specifics.
---

# SEO Optimization — Next.js App Router

Scope: `apps/frontend/<customer-app>` in the mono-repo template (Next.js + Tailwind). Does not apply to the admin portal (React/Vite, not indexed) unless explicitly asked.

## Workflow

1. **Locate the app.** Confirm the target directory, e.g. `apps/frontend/customer`. Read `app/layout.tsx` and `next.config.ts` first.
2. **Run the audit checklist below** against the current codebase — read files, don't assume. Use `grep`/`rg` to find gaps (missing `alt`, missing `generateMetadata`, etc.).
3. **Report findings as a checklist** (✅ / ⚠️ / ❌) before making changes, unless the user asked you to just fix everything directly.
4. **Fix in priority order**: metadata → sitemap/robots → structured data → Open Graph → images → Core Web Vitals → semantic HTML. Content structure / topical authority (section 8) is assessed separately — it's editorial, not a code fix, so surface it as recommendations rather than auto-generating content.
5. Keep changes TypeScript-strict, no `any`, minimal comments — consistent with the rest of the repo.

---

## 1. Metadata (highest priority)

Next.js App Router uses the Metadata API, not `<head>` tags or `next/head`.

**Root layout** (`app/layout.tsx`) — sets defaults and a title template:

```typescript
import type { Metadata } from "next";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_CUSTOMER_SITE_URL!),
  title: {
    default: "Business Name — Short Value Prop",
    template: "%s | Business Name",
  },
  description: "One or two sentences, 150–160 characters, unique per app.",
  alternates: { canonical: "/" },
  robots: { index: true, follow: true },
};
```

**Per-page** — use `generateMetadata` for dynamic routes (e.g. service pages, blog posts), static `export const metadata` for fixed pages:

```typescript
import type { Metadata } from "next";

export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> {
  const { slug } = await params;
  const item = await getItem(slug);

  return {
    title: item.name,
    description: item.shortDescription,
    alternates: { canonical: `/services/${slug}` },
  };
}
```

Checklist:
- ⚠️ Every route has a unique `title` and `description` — no duplicates across pages.
- ⚠️ `metadataBase` is set once in the root layout so relative OG/canonical URLs resolve correctly.
- ⚠️ `NEXT_PUBLIC_CUSTOMER_SITE_URL` is set as a build-time env var per Coolify environment (it's baked in at build, not runtime, since Base Directory is repo root and the Dockerfile builds standalone output).

---

## 2. Sitemap & robots.txt

Use the file-convention APIs — don't hand-roll static files.

`app/sitemap.ts`:

```typescript
import type { MetadataRoute } from "next";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const baseUrl = process.env.NEXT_PUBLIC_CUSTOMER_SITE_URL!;
  const items = await getAllPublishedItems();

  const staticRoutes: MetadataRoute.Sitemap = ["", "/services", "/contact"].map((path) => ({
    url: `${baseUrl}${path}`,
    lastModified: new Date(),
  }));

  const dynamicRoutes: MetadataRoute.Sitemap = items.map((item) => ({
    url: `${baseUrl}/services/${item.slug}`,
    lastModified: item.updatedAt,
  }));

  return [...staticRoutes, ...dynamicRoutes];
}
```

`app/robots.ts`:

```typescript
import type { MetadataRoute } from "next";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: "/" },
    sitemap: `${process.env.NEXT_PUBLIC_CUSTOMER_SITE_URL}/sitemap.xml`,
  };
}
```

Checklist:
- ❌ Never let `robots.ts` disallow everything on a production customer-facing build (common accident when a staging env var leaks into prod).
- ⚠️ Dynamic routes (services, listings, blog posts) are pulled from the DB, not hardcoded — otherwise the sitemap goes stale.

---

## 3. Structured data (JSON-LD)

Almost every customer-facing app in this portfolio is a **local service business** (garden services, mobile car wash, medical practice) — `LocalBusiness` schema is high-value here and often skipped.

```typescript
export function LocalBusinessJsonLd() {
  const data = {
    "@context": "https://schema.org",
    "@type": "LocalBusiness",
    name: "Business Name",
    image: "https://example.co.za/logo.png",
    telephone: "+27...",
    address: {
      "@type": "PostalAddress",
      streetAddress: "...",
      addressLocality: "...",
      addressRegion: "KZN",
      postalCode: "....",
      addressCountry: "ZA",
    },
    areaServed: "Durban, KZN",
    priceRange: "$$",
  };

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(data) }}
    />
  );
}
```

Other schema types worth checking for by app type:
- Booking/quote flow → `Service` + `Offer`
- Reviews shown on-page → `AggregateRating` (only if reviews are real and displayed — never fabricate ratings, this violates Google's structured data guidelines)
- Blog/content pages → `Article` or `BlogPosting`

Checklist:
- ⚠️ JSON-LD is rendered server-side (not client-injected after hydration) so crawlers see it.
- ⚠️ Validate with Google's Rich Results Test before shipping.

---

## 4. Open Graph & Twitter Cards

Extend the Metadata API object rather than adding separate meta tags:

```typescript
export const metadata: Metadata = {
  openGraph: {
    title: "...",
    description: "...",
    url: "/",
    siteName: "Business Name",
    images: [{ url: "/og-image.png", width: 1200, height: 630 }],
    locale: "en_ZA",
    type: "website",
  },
  twitter: {
    card: "summary_large_image",
    title: "...",
    description: "...",
    images: ["/og-image.png"],
  },
};
```

Checklist:
- ⚠️ OG image is a real static asset (1200×630), not a screenshot — check `public/`.
- ⚠️ `locale: "en_ZA"` set where relevant for local South African businesses.

---

## 5. Images & media

- ⚠️ Every `<img>` should be `next/image` — flag any raw `<img>` tags found via grep.
- ❌ No missing/empty `alt` attributes. Decorative images use `alt=""`, never omit it.
- ⚠️ `next/image` `sizes` prop set correctly for responsive images, not just `width`/`height`, or CLS suffers.

---

## 6. Core Web Vitals / performance

These affect SEO ranking directly (Google's page experience signals).

- ⚠️ `next/font` used for all fonts (not `<link>` to Google Fonts) — avoids render-blocking and layout shift.
- ⚠️ Largest Contentful Paint element (usually hero image) is preloaded / marked `priority` on `next/image`.
- ⚠️ No unbounded client-side data fetching on the initial render path for content that could be server-rendered — hurts both LCP and crawlability.
- If Lighthouse/PageSpeed Insights is available, run it and report Core Web Vitals scores before/after.

---

## 7. Semantic HTML & URL structure

- ⚠️ One `<h1>` per page, logical heading order (no skipping levels).
- ⚠️ Nav/footer use `<nav>`, `<footer>`, `<main>` — not generic `<div>` soup.
- ⚠️ URLs are human-readable and kebab-case (`/services/garden-maintenance`, not `/services?id=4`).
- ⚠️ Trailing slash / www vs non-www is consistent — check `next.config.ts` redirects and DNS/Cloudflare CNAME setup so canonical and actual served URL match.

---

## 8. Content structure & topical authority

Technical SEO gets a page crawled and indexed. Topical authority is what makes it rank against local competitors who've been publishing longer — this is the part most technical audits skip.

- **Pillar/cluster structure**: one broad pillar page per core service category (e.g. "Garden Maintenance"), linking out to narrower cluster pages targeting specific queries ("Lawn Mowing", "Hedge Trimming", "Irrigation Repair"). Clusters link back to the pillar; related clusters cross-link.
- **Internal links use `next/link`**, not plain `<a>` or JS-only navigation — this is what makes the link graph crawlable, not just clickable.
- **Location depth, not a thin area list**: for local businesses, real per-suburb content ("Garden Services in Umhlanga") outperforms one generic "areas we serve" page. A templated paragraph with the suburb name swapped in counts as thin/duplicate content and can hurt more than help.
- **FAQ content + `FAQPage` schema**: genuine answers to real customer questions — captures long-tail queries and can win a SERP rich-result feature.
- **E-E-A-T (Experience, Expertise, Authoritativeness, Trust)**: matters most for YMYL content — a medical practice site needs visible practitioner credentials and registration numbers, not just a booking form. Testimonials/reviews must be real and attributable (never fabricate — ties back to the `AggregateRating` warning in section 3).
- **Avoid thin pages**: a service page with a heading, a stock photo, and a lead-gen form has little for Google to rank on. Flag pages under ~150–200 words of substantive unique copy.

When auditing, call out thin/duplicate content and missing pillar structure as recommendations for the user to action (write copy, define the cluster map) — don't generate marketing copy unprompted.

---

## Reporting format

When auditing, report like this:

```
## SEO Audit — apps/frontend/customer

✅ Root metadata configured
⚠️ 3 pages missing generateMetadata (services/[slug], blog/[slug], contact)
❌ No sitemap.ts found
❌ robots.ts allows all but sitemap URL uses wrong env var
⚠️ 4 <img> tags not using next/image (components/Hero.tsx, components/Gallery.tsx)
✅ LocalBusiness JSON-LD present on homepage, missing on /contact
⚠️ No pillar/cluster structure — 6 service pages exist as flat siblings with no cross-linking
❌ /areas-we-served uses one templated paragraph per suburb — thin/duplicate content risk
```

Then fix in priority order from the workflow above, confirming scope with the user first if the fix set is large.
