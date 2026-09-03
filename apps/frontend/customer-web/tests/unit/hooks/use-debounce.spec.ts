import { renderHook, act } from "@testing-library/react";
import { useDebounce } from "@/hooks/use-debounce";

describe("useDebounce", () => {
    beforeEach(() => {
        jest.useFakeTimers();
    });

    afterEach(() => {
        jest.useRealTimers();
    });

    it("returns the initial value immediately", () => {
        const { result } = renderHook(() => useDebounce("initial", 500));
        expect(result.current).toBe("initial");
    });

    it("updates the value once the delay has elapsed", () => {
        const { result, rerender } = renderHook(({ value }) => useDebounce(value, 500), {
            initialProps: { value: "first" },
        });

        rerender({ value: "second" });
        act(() => {
            jest.advanceTimersByTime(500);
        });

        expect(result.current).toBe("second");
    });

    it("does not update the value before the delay elapses", () => {
        const { result, rerender } = renderHook(({ value }) => useDebounce(value, 500), {
            initialProps: { value: "first" },
        });

        rerender({ value: "second" });
        act(() => {
            jest.advanceTimersByTime(400);
        });

        expect(result.current).toBe("first");
    });
});
