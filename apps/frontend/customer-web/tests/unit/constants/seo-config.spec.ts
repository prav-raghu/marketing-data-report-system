import { buildCanonicalUrl, buildImageUrl, formatTitle, SEO_CONFIG } from "@/constants/seo-config";

describe("seo-config", () => {
    describe("formatTitle", () => {
        it("returns the default title when no page title is given", () => {
            expect(formatTitle()).toBe(SEO_CONFIG.defaultTitle);
        });

        it("interpolates the page title into the template", () => {
            expect(formatTitle("Pricing")).toBe(`Pricing | ${SEO_CONFIG.defaultTitle}`);
        });
    });

    describe("buildCanonicalUrl", () => {
        it("prefixes the path with the site url", () => {
            expect(buildCanonicalUrl("/about")).toBe(`${SEO_CONFIG.siteUrl}/about`);
        });
    });

    describe("buildImageUrl", () => {
        it("prefixes a relative path with the site url", () => {
            expect(buildImageUrl("/og.jpg")).toBe(`${SEO_CONFIG.siteUrl}/og.jpg`);
        });

        it("returns an absolute url unchanged", () => {
            expect(buildImageUrl("https://cdn.test/og.jpg")).toBe("https://cdn.test/og.jpg");
        });
    });
});
