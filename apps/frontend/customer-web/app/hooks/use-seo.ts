"use client";

import { usePathname } from "next/navigation";
import { SEO_CONFIG, buildCanonicalUrl } from "../constants/seo-config";

interface SEOData {
    title: string;
    description: string;
    canonicalUrl: string;
    ogUrl: string;
    path: string;
}

export function useSEO(): SEOData {
    const pathname = usePathname();

    return {
        title: SEO_CONFIG.defaultTitle,
        description: SEO_CONFIG.defaultDescription,
        canonicalUrl: buildCanonicalUrl(pathname),
        ogUrl: buildCanonicalUrl(pathname),
        path: pathname,
    };
}
