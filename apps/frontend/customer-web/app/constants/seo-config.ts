export const SEO_CONFIG = {
    defaultTitle: "Customer Web App",
    titleTemplate: "%s | Customer Web App",
    defaultDescription: "A production-ready customer-facing web application built with React, TypeScript, and Tailwind CSS.",
    siteUrl: "https://example.com",
    defaultImage: "/og-image.jpg",
    twitterHandle: "@example",
    facebookAppId: "",
    author: "Your Company Name",
    keywords: ["react", "typescript", "web application", "customer portal"],
};

export function formatTitle(pageTitle?: string): string {
    if (!pageTitle) {
        return SEO_CONFIG.defaultTitle;
    }
    return SEO_CONFIG.titleTemplate.replace("%s", pageTitle);
}

export function buildCanonicalUrl(path: string): string {
    return `${SEO_CONFIG.siteUrl}${path}`;
}

export function buildImageUrl(imagePath: string): string {
    if (imagePath.startsWith("http")) {
        return imagePath;
    }
    return `${SEO_CONFIG.siteUrl}${imagePath}`;
}
