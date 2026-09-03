import { QUERY_KEYS } from "@/constants/query-keys";

describe("QUERY_KEYS", () => {
    it("exposes the expected query key values", () => {
        expect(QUERY_KEYS.USER).toBe("user");
        expect(QUERY_KEYS.USERS).toBe("users");
        expect(QUERY_KEYS.PROFILE).toBe("profile");
    });
});
