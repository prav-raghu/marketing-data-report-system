import { useApiStore } from "../../../src/store/use-api-store";

describe("useApiStore", () => {
    afterEach(() => {
        useApiStore.getState().reset();
    });

    it("starts with no data, not loading, and no error", () => {
        const state = useApiStore.getState();
        expect(state.data).toBeNull();
        expect(state.loading).toBe(false);
        expect(state.error).toBeNull();
    });

    it("sets data and clears any existing error", () => {
        useApiStore.getState().setError("boom");
        useApiStore.getState().setData({ id: 1, title: "t", body: "b" });

        const state = useApiStore.getState();
        expect(state.data).toEqual({ id: 1, title: "t", body: "b" });
        expect(state.error).toBeNull();
    });

    it("sets the loading flag", () => {
        useApiStore.getState().setLoading(true);
        expect(useApiStore.getState().loading).toBe(true);
    });

    it("sets an error and clears the loading flag", () => {
        useApiStore.getState().setLoading(true);
        useApiStore.getState().setError("network error");

        const state = useApiStore.getState();
        expect(state.error).toBe("network error");
        expect(state.loading).toBe(false);
    });

    it("resets to the initial state", () => {
        useApiStore.getState().setData({ id: 1, title: "t", body: "b" });
        useApiStore.getState().setLoading(true);

        useApiStore.getState().reset();

        const state = useApiStore.getState();
        expect(state.data).toBeNull();
        expect(state.loading).toBe(false);
        expect(state.error).toBeNull();
    });
});
