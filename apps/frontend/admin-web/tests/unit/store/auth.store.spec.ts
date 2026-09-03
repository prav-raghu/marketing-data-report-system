import { useAuthStore } from "../../../src/store/auth.store";
import { authTokenStore } from "../../../src/store/auth-token.store";

describe("useAuthStore", () => {
    afterEach(() => {
        useAuthStore.getState().clearAuth();
    });

    it("starts unauthenticated with no user", () => {
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().user).toBeNull();
    });

    it("sets the authenticated state and stores the token", () => {
        const user = { id: "u1", username: "alice", email: "alice@test.com", role: "Super Admin" };

        useAuthStore.getState().setAuth(user, "access-token");

        expect(useAuthStore.getState().isAuthenticated).toBe(true);
        expect(useAuthStore.getState().user).toEqual(user);
        expect(authTokenStore.getToken()).toBe("access-token");
    });

    it("clears the authenticated state and the token", () => {
        const user = { id: "u1", username: "alice", email: "alice@test.com", role: "Super Admin" };
        useAuthStore.getState().setAuth(user, "access-token");

        useAuthStore.getState().clearAuth();

        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().user).toBeNull();
        expect(authTokenStore.getToken()).toBeNull();
    });
});
