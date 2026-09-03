import { useCounterStore } from "../../../src/store/use-counter-store";

describe("useCounterStore", () => {
    afterEach(() => {
        useCounterStore.getState().reset();
    });

    it("starts at zero", () => {
        expect(useCounterStore.getState().count).toBe(0);
    });

    it("increments the count", () => {
        useCounterStore.getState().increment();
        expect(useCounterStore.getState().count).toBe(1);
    });

    it("decrements the count", () => {
        useCounterStore.getState().setCount(5);
        useCounterStore.getState().decrement();
        expect(useCounterStore.getState().count).toBe(4);
    });

    it("resets the count to zero", () => {
        useCounterStore.getState().setCount(10);
        useCounterStore.getState().reset();
        expect(useCounterStore.getState().count).toBe(0);
    });

    it("sets an arbitrary count", () => {
        useCounterStore.getState().setCount(42);
        expect(useCounterStore.getState().count).toBe(42);
    });
});
