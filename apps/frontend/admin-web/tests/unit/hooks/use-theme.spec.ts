import { renderHook, act } from "@testing-library/react";
import { useTheme } from "../../../src/hooks/use-theme";

function mockMatchMedia(matches: boolean) {
    const listeners: ((event: MediaQueryListEvent) => void)[] = [];
    return {
        matches,
        addEventListener: jest.fn((_event: string, handler: (event: MediaQueryListEvent) => void) => {
            listeners.push(handler);
        }),
        removeEventListener: jest.fn(),
        dispatch: () => listeners.forEach((handler) => handler({} as MediaQueryListEvent)),
    };
}

describe("useTheme", () => {
    beforeEach(() => {
        localStorage.clear();
        document.documentElement.removeAttribute("data-theme");
    });

    it("defaults to system when nothing is stored", () => {
        globalThis.matchMedia = jest.fn().mockReturnValue(mockMatchMedia(false)) as never;
        const { result } = renderHook(() => useTheme());
        expect(result.current.theme).toBe("system");
    });

    it("reads a previously stored theme", () => {
        localStorage.setItem("theme", "dark");
        globalThis.matchMedia = jest.fn().mockReturnValue(mockMatchMedia(false)) as never;

        const { result } = renderHook(() => useTheme());

        expect(result.current.theme).toBe("dark");
    });

    it("applies the explicit theme to the document root", () => {
        globalThis.matchMedia = jest.fn().mockReturnValue(mockMatchMedia(false)) as never;
        const { result } = renderHook(() => useTheme());

        act(() => {
            result.current.setTheme("dark");
        });

        expect(document.documentElement.dataset.theme).toBe("dark");
        expect(localStorage.getItem("theme")).toBe("dark");
    });

    it("resolves system theme to dark when the OS prefers dark", () => {
        globalThis.matchMedia = jest.fn().mockReturnValue(mockMatchMedia(true)) as never;
        const { result } = renderHook(() => useTheme());

        act(() => {
            result.current.setTheme("system");
        });

        expect(document.documentElement.dataset.theme).toBe("dark");
    });

    it("resolves system theme to light when the OS prefers light", () => {
        globalThis.matchMedia = jest.fn().mockReturnValue(mockMatchMedia(false)) as never;
        const { result } = renderHook(() => useTheme());

        act(() => {
            result.current.setTheme("system");
        });

        expect(document.documentElement.dataset.theme).toBe("light");
    });
});
