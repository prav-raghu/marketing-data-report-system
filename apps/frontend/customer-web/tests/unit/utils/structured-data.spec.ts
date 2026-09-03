import {
    createArticleSchema,
    createBreadcrumbSchema,
    createFAQSchema,
    createOrganizationSchema,
    createProductSchema,
    createWebSiteSchema,
} from "@/utils/structured-data";

describe("structured-data", () => {
    describe("createOrganizationSchema", () => {
        it("builds the minimal required schema", () => {
            const schema = createOrganizationSchema({ name: "Acme", url: "https://acme.test" });
            expect(schema).toEqual({
                "@context": "https://schema.org",
                "@type": "Organization",
                name: "Acme",
                url: "https://acme.test",
            });
        });

        it("includes optional fields when provided", () => {
            const schema = createOrganizationSchema({
                name: "Acme",
                url: "https://acme.test",
                logo: "https://acme.test/logo.png",
                socialLinks: ["https://twitter.com/acme"],
                contactPhone: "+1-555-0100",
                contactEmail: "hello@acme.test",
            });

            expect(schema.logo).toBe("https://acme.test/logo.png");
            expect(schema.sameAs).toEqual(["https://twitter.com/acme"]);
            expect(schema.contactPoint).toEqual({
                "@type": "ContactPoint",
                contactType: "customer service",
                telephone: "+1-555-0100",
                email: "hello@acme.test",
            });
        });

        it("omits sameAs when socialLinks is empty", () => {
            const schema = createOrganizationSchema({ name: "Acme", url: "https://acme.test", socialLinks: [] });
            expect(schema.sameAs).toBeUndefined();
        });

        it("includes only the phone when just contactPhone is provided", () => {
            const schema = createOrganizationSchema({ name: "Acme", url: "https://acme.test", contactPhone: "+1-555-0100" });
            expect(schema.contactPoint).toEqual({
                "@type": "ContactPoint",
                contactType: "customer service",
                telephone: "+1-555-0100",
            });
        });

        it("includes only the email when just contactEmail is provided", () => {
            const schema = createOrganizationSchema({ name: "Acme", url: "https://acme.test", contactEmail: "hello@acme.test" });
            expect(schema.contactPoint).toEqual({
                "@type": "ContactPoint",
                contactType: "customer service",
                email: "hello@acme.test",
            });
        });
    });

    describe("createWebSiteSchema", () => {
        it("builds the schema without a search action by default", () => {
            const schema = createWebSiteSchema({ name: "Acme", url: "https://acme.test" });
            expect(schema.potentialAction).toBeUndefined();
        });

        it("adds a search action when searchUrl is provided", () => {
            const schema = createWebSiteSchema({
                name: "Acme",
                url: "https://acme.test",
                searchUrl: "https://acme.test/search",
            });

            expect(schema.potentialAction).toEqual({
                "@type": "SearchAction",
                target: "https://acme.test/search?q={search_term_string}",
                "query-input": "required name=search_term_string",
            });
        });
    });

    describe("createBreadcrumbSchema", () => {
        it("maps breadcrumbs to positioned list items", () => {
            const schema = createBreadcrumbSchema([{ name: "Home", url: "https://acme.test" }, { name: "About" }]);

            expect(schema.itemListElement).toEqual([
                { "@type": "ListItem", position: 1, name: "Home", item: "https://acme.test" },
                { "@type": "ListItem", position: 2, name: "About", item: undefined },
            ]);
        });
    });

    describe("createArticleSchema", () => {
        it("builds the minimal schema", () => {
            const schema = createArticleSchema({ headline: "Breaking news" });
            expect(schema).toEqual({
                "@context": "https://schema.org",
                "@type": "Article",
                headline: "Breaking news",
            });
        });

        it("includes author and publisher when provided", () => {
            const schema = createArticleSchema({
                headline: "Breaking news",
                description: "A summary",
                image: "https://acme.test/article.png",
                authorName: "Jane Doe",
                publisherName: "Acme News",
                publisherLogo: "https://acme.test/logo.png",
                datePublished: "2026-01-01",
                dateModified: "2026-01-02",
            });

            expect(schema.description).toBe("A summary");
            expect(schema.image).toBe("https://acme.test/article.png");
            expect(schema.author).toEqual({ "@type": "Person", name: "Jane Doe" });
            expect(schema.publisher).toEqual({
                "@type": "Organization",
                name: "Acme News",
                logo: { "@type": "ImageObject", url: "https://acme.test/logo.png" },
            });
            expect(schema.datePublished).toBe("2026-01-01");
            expect(schema.dateModified).toBe("2026-01-02");
        });

        it("omits the publisher logo when only the publisher name is provided", () => {
            const schema = createArticleSchema({ headline: "Breaking news", publisherName: "Acme News" });
            expect(schema.publisher).toEqual({ "@type": "Organization", name: "Acme News" });
        });
    });

    describe("createProductSchema", () => {
        it("builds the minimal schema", () => {
            const schema = createProductSchema({ name: "Widget" });
            expect(schema.offers).toBeUndefined();
            expect(schema.aggregateRating).toBeUndefined();
        });

        it("includes offer and rating details when provided", () => {
            const schema = createProductSchema({
                name: "Widget",
                image: "https://acme.test/widget.png",
                description: "A great widget",
                brandName: "Acme",
                price: "19.99",
                currency: "USD",
                availability: "InStock",
                offerUrl: "https://acme.test/widget",
                rating: 4.5,
                reviewCount: 10,
            });

            expect(schema.image).toBe("https://acme.test/widget.png");
            expect(schema.description).toBe("A great widget");
            expect(schema.brand).toEqual({ "@type": "Brand", name: "Acme" });
            expect(schema.offers).toEqual({
                "@type": "Offer",
                url: "https://acme.test/widget",
                priceCurrency: "USD",
                price: "19.99",
                availability: "InStock",
            });
            expect(schema.aggregateRating).toEqual({
                "@type": "AggregateRating",
                ratingValue: 4.5,
                reviewCount: 10,
            });
        });

        it("does not include aggregateRating when reviewCount is missing", () => {
            const schema = createProductSchema({ name: "Widget", rating: 4.5 });
            expect(schema.aggregateRating).toBeUndefined();
        });

        it("builds an offer with only the offerUrl set", () => {
            const schema = createProductSchema({ name: "Widget", offerUrl: "https://acme.test/widget" });
            expect(schema.offers).toEqual({ "@type": "Offer", url: "https://acme.test/widget" });
        });

        it("builds an offer with only the currency set", () => {
            const schema = createProductSchema({ name: "Widget", currency: "USD" });
            expect(schema.offers).toEqual({ "@type": "Offer", priceCurrency: "USD" });
        });

        it("builds an offer with only the price set", () => {
            const schema = createProductSchema({ name: "Widget", price: "9.99" });
            expect(schema.offers).toEqual({ "@type": "Offer", price: "9.99" });
        });

        it("builds an offer with only the availability set", () => {
            const schema = createProductSchema({ name: "Widget", availability: "OutOfStock" });
            expect(schema.offers).toEqual({ "@type": "Offer", availability: "OutOfStock" });
        });
    });

    describe("createFAQSchema", () => {
        it("maps question/answer pairs into FAQPage entities", () => {
            const schema = createFAQSchema([{ question: "What is this?", answer: "A demo." }]);

            expect(schema.mainEntity).toEqual([
                {
                    "@type": "Question",
                    name: "What is this?",
                    acceptedAnswer: { "@type": "Answer", text: "A demo." },
                },
            ]);
        });
    });
});
