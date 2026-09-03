import { renderHook, act } from "@testing-library/react";
import { useDebounce } from "../../../src/hooks/use-debounce";

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

    it("resets the timer when the value changes again before the delay elapses", () => {
        const { result, rerender } = renderHook(({ value }) => useDebounce(value, 500), {
            initialProps: { value: "first" },
        });

        rerender({ value: "second" });
        act(() => {
            jest.advanceTimersByTime(300);
        });
        rerender({ value: "third" });
        act(() => {
            jest.advanceTimersByTime(300);
        });

        expect(result.current).toBe("first");

        act(() => {
            jest.advanceTimersByTime(200);
        });

        expect(result.current).toBe("third");
    });
});
