import { capitalize, slugify, truncate } from "../../../src/utils/string";

describe("string utilities", () => {
    describe("truncate", () => {
        it("truncates a string longer than the given length and appends an ellipsis", () => {
            expect(truncate("hello world", 5)).toBe("hello...");
        });

        it("returns the string unchanged when shorter than the length", () => {
            expect(truncate("hi", 5)).toBe("hi");
        });

        it("returns the string unchanged when exactly the given length", () => {
            expect(truncate("hello", 5)).toBe("hello");
        });
    });

    describe("capitalize", () => {
        it("capitalizes the first character", () => {
            expect(capitalize("hello")).toBe("Hello");
        });

        it("leaves an already-capitalized string unchanged", () => {
            expect(capitalize("Hello")).toBe("Hello");
        });
    });

    describe("slugify", () => {
        it("lowercases and hyphenates spaces", () => {
            expect(slugify("Hello World")).toBe("hello-world");
        });

        it("strips special characters", () => {
            expect(slugify("Hello, World!")).toBe("hello-world");
        });

        it("collapses repeated separators", () => {
            expect(slugify("hello   ---  world")).toBe("hello-world");
        });

        it("trims leading and trailing hyphens", () => {
            expect(slugify("  -Hello World-  ")).toBe("hello-world");
        });
    });
});
