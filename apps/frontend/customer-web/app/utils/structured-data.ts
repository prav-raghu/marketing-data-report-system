interface Organization {
    "@context": "https://schema.org";
    "@type": "Organization";
    name: string;
    url: string;
    logo?: string;
    sameAs?: string[];
    contactPoint?: {
        "@type": "ContactPoint";
        telephone?: string;
        contactType?: string;
        email?: string;
    };
}

interface WebSite {
    "@context": "https://schema.org";
    "@type": "WebSite";
    name: string;
    url: string;
    potentialAction?: {
        "@type": "SearchAction";
        target: string;
        "query-input": string;
    };
}

interface BreadcrumbList {
    "@context": "https://schema.org";
    "@type": "BreadcrumbList";
    itemListElement: {
        "@type": "ListItem";
        position: number;
        name: string;
        item?: string;
    }[];
}

interface Article {
    "@context": "https://schema.org";
    "@type": "Article";
    headline: string;
    description?: string;
    image?: string;
    author?: {
        "@type": "Person" | "Organization";
        name: string;
    };
    publisher?: {
        "@type": "Organization";
        name: string;
        logo?: {
            "@type": "ImageObject";
            url: string;
        };
    };
    datePublished?: string;
    dateModified?: string;
}

interface Product {
    "@context": "https://schema.org";
    "@type": "Product";
    name: string;
    image?: string | string[];
    description?: string;
    brand?: {
        "@type": "Brand";
        name: string;
    };
    offers?: {
        "@type": "Offer";
        url?: string;
        priceCurrency?: string;
        price?: string;
        availability?: string;
    };
    aggregateRating?: {
        "@type": "AggregateRating";
        ratingValue: number;
        reviewCount: number;
    };
}

interface FAQPage {
    "@context": "https://schema.org";
    "@type": "FAQPage";
    mainEntity: {
        "@type": "Question";
        name: string;
        acceptedAnswer: {
            "@type": "Answer";
            text: string;
        };
    }[];
}

export function createOrganizationSchema(data: {
    name: string;
    url: string;
    logo?: string;
    socialLinks?: string[];
    contactPhone?: string;
    contactEmail?: string;
}): Organization {
    const schema: Organization = {
        "@context": "https://schema.org",
        "@type": "Organization",
        name: data.name,
        url: data.url,
    };

    if (data.logo) {
        schema.logo = data.logo;
    }

    if (data.socialLinks && data.socialLinks.length > 0) {
        schema.sameAs = data.socialLinks;
    }

    if (data.contactPhone || data.contactEmail) {
        schema.contactPoint = {
            "@type": "ContactPoint",
            contactType: "customer service",
        };
        if (data.contactPhone) {
            schema.contactPoint.telephone = data.contactPhone;
        }
        if (data.contactEmail) {
            schema.contactPoint.email = data.contactEmail;
        }
    }

    return schema;
}

export function createWebSiteSchema(data: { name: string; url: string; searchUrl?: string }): WebSite {
    const schema: WebSite = {
        "@context": "https://schema.org",
        "@type": "WebSite",
        name: data.name,
        url: data.url,
    };

    if (data.searchUrl) {
        schema.potentialAction = {
            "@type": "SearchAction",
            target: `${data.searchUrl}?q={search_term_string}`,
            "query-input": "required name=search_term_string",
        };
    }

    return schema;
}

export function createBreadcrumbSchema(breadcrumbs: { name: string; url?: string }[]): BreadcrumbList {
    return {
        "@context": "https://schema.org",
        "@type": "BreadcrumbList",
        itemListElement: breadcrumbs.map((crumb, index) => ({
            "@type": "ListItem",
            position: index + 1,
            name: crumb.name,
            item: crumb.url,
        })),
    };
}

export function createArticleSchema(data: {
    headline: string;
    description?: string;
    image?: string;
    authorName?: string;
    publisherName?: string;
    publisherLogo?: string;
    datePublished?: string;
    dateModified?: string;
}): Article {
    const schema: Article = {
        "@context": "https://schema.org",
        "@type": "Article",
        headline: data.headline,
    };

    if (data.description) {
        schema.description = data.description;
    }

    if (data.image) {
        schema.image = data.image;
    }

    if (data.authorName) {
        schema.author = {
            "@type": "Person",
            name: data.authorName,
        };
    }

    if (data.publisherName) {
        schema.publisher = {
            "@type": "Organization",
            name: data.publisherName,
        };

        if (data.publisherLogo) {
            schema.publisher.logo = {
                "@type": "ImageObject",
                url: data.publisherLogo,
            };
        }
    }

    if (data.datePublished) {
        schema.datePublished = data.datePublished;
    }

    if (data.dateModified) {
        schema.dateModified = data.dateModified;
    }

    return schema;
}

export function createProductSchema(data: {
    name: string;
    image?: string | string[];
    description?: string;
    brandName?: string;
    offerUrl?: string;
    price?: string;
    currency?: string;
    availability?: string;
    rating?: number;
    reviewCount?: number;
}): Product {
    const schema: Product = {
        "@context": "https://schema.org",
        "@type": "Product",
        name: data.name,
    };

    if (data.image) {
        schema.image = data.image;
    }

    if (data.description) {
        schema.description = data.description;
    }

    if (data.brandName) {
        schema.brand = {
            "@type": "Brand",
            name: data.brandName,
        };
    }

    if (data.price || data.currency || data.availability || data.offerUrl) {
        schema.offers = {
            "@type": "Offer",
        };

        if (data.offerUrl) {
            schema.offers.url = data.offerUrl;
        }

        if (data.currency) {
            schema.offers.priceCurrency = data.currency;
        }

        if (data.price) {
            schema.offers.price = data.price;
        }

        if (data.availability) {
            schema.offers.availability = data.availability;
        }
    }

    if (data.rating && data.reviewCount) {
        schema.aggregateRating = {
            "@type": "AggregateRating",
            ratingValue: data.rating,
            reviewCount: data.reviewCount,
        };
    }

    return schema;
}

export function createFAQSchema(faqs: { question: string; answer: string }[]): FAQPage {
    return {
        "@context": "https://schema.org",
        "@type": "FAQPage",
        mainEntity: faqs.map((faq) => ({
            "@type": "Question",
            name: faq.question,
            acceptedAnswer: {
                "@type": "Answer",
                text: faq.answer,
            },
        })),
    };
}
