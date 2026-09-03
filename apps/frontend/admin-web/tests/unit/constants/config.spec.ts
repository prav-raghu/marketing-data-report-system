describe("config", () => {
    const originalEnv = process.env;

    afterEach(() => {
        process.env = originalEnv;
        jest.resetModules();
    });

    it("falls back to defaults when env vars are unset", () => {
        process.env = { ...originalEnv, VITE_ADMIN_API_BASE_URL: "", VITE_ADMIN_APP_NAME: "" };
        const { API_BASE_URL, APP_NAME } = require("../../../src/constants/config");
        expect(API_BASE_URL).toBe("/api");
        expect(APP_NAME).toBe("Admin");
    });

    it("uses the configured env vars when present", () => {
        process.env = {
            ...originalEnv,
            VITE_ADMIN_API_BASE_URL: "https://api.example.com",
            VITE_ADMIN_APP_NAME: "My Admin",
        };
        const { API_BASE_URL, APP_NAME } = require("../../../src/constants/config");
        expect(API_BASE_URL).toBe("https://api.example.com");
        expect(APP_NAME).toBe("My Admin");
    });
});
