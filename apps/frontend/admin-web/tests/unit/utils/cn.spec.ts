import { cn } from "../../../src/utils/cn";

describe("cn", () => {
    it("merges plain class name strings", () => {
        expect(cn("px-2", "py-4")).toBe("px-2 py-4");
    });

    it("drops falsy values", () => {
        expect(cn("px-2", false, undefined, null, "py-4")).toBe("px-2 py-4");
    });

    it("resolves conflicting tailwind classes to the last one", () => {
        expect(cn("px-2", "px-4")).toBe("px-4");
    });

    it("supports conditional object syntax", () => {
        expect(cn("base", { active: true, disabled: false })).toBe("base active");
    });
});
