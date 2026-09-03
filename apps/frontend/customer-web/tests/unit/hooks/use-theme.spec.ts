import { renderHook, act } from "@testing-library/react";
import { useTheme } from "@/hooks/use-theme";

function mockMatchMedia(matches: boolean) {
    return {
        matches,
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
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

    it("re-applies the system theme when the OS preference change fires", () => {
        const media = mockMatchMedia(true);
        globalThis.matchMedia = jest.fn().mockReturnValue(media) as never;

        renderHook(() => useTheme());

        const [, handler] = media.addEventListener.mock.calls[0];
        act(() => {
            handler();
        });

        expect(document.documentElement.dataset.theme).toBe("dark");
    });

    it("removes the change listener on unmount while in system mode", () => {
        const media = mockMatchMedia(false);
        globalThis.matchMedia = jest.fn().mockReturnValue(media) as never;

        const { unmount } = renderHook(() => useTheme());
        unmount();

        expect(media.removeEventListener).toHaveBeenCalledWith("change", expect.any(Function));
    });
});
