jest.mock("next/navigation", () => ({
    usePathname: jest.fn(),
}));

import { renderHook } from "@testing-library/react";
import { usePathname } from "next/navigation";
import { useSEO } from "@/hooks/use-seo";
import { SEO_CONFIG } from "@/constants/seo-config";

describe("useSEO", () => {
    it("builds SEO metadata from the current pathname", () => {
        (usePathname as jest.Mock).mockReturnValue("/about");

        const { result } = renderHook(() => useSEO());

        expect(result.current.title).toBe(SEO_CONFIG.defaultTitle);
        expect(result.current.description).toBe(SEO_CONFIG.defaultDescription);
        expect(result.current.canonicalUrl).toBe(`${SEO_CONFIG.siteUrl}/about`);
        expect(result.current.ogUrl).toBe(`${SEO_CONFIG.siteUrl}/about`);
        expect(result.current.path).toBe("/about");
    });
});
