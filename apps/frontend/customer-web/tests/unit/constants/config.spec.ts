import { API_BASE_URL, APP_NAME } from "@/constants/config";

describe("config", () => {
    it("exposes an API base url", () => {
        expect(typeof API_BASE_URL).toBe("string");
        expect(API_BASE_URL.length).toBeGreaterThan(0);
    });

    it("exposes an app name", () => {
        expect(typeof APP_NAME).toBe("string");
        expect(APP_NAME.length).toBeGreaterThan(0);
    });
});
