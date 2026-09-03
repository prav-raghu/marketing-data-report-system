import { authTokenStore } from "@/store/auth-token.store";

describe("authTokenStore", () => {
    afterEach(() => {
        authTokenStore.clearToken();
    });

    it("returns null when no token has been set", () => {
        expect(authTokenStore.getToken()).toBeNull();
    });

    it("stores and returns a token", () => {
        authTokenStore.setToken("abc123");
        expect(authTokenStore.getToken()).toBe("abc123");
    });

    it("overwrites a previously stored token", () => {
        authTokenStore.setToken("first");
        authTokenStore.setToken("second");
        expect(authTokenStore.getToken()).toBe("second");
    });

    it("clears the stored token", () => {
        authTokenStore.setToken("abc123");
        authTokenStore.clearToken();
        expect(authTokenStore.getToken()).toBeNull();
    });
});
