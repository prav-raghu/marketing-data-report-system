import { HTTP_STATUS } from "../../../src/constants/http-status";
import { QUERY_KEYS } from "../../../src/constants/query-keys";
import { ROUTES } from "../../../src/constants/routes";

describe("constants", () => {
    it("exposes the expected HTTP status codes", () => {
        expect(HTTP_STATUS.OK).toBe(200);
        expect(HTTP_STATUS.NOT_FOUND).toBe(404);
        expect(HTTP_STATUS.INTERNAL_SERVER_ERROR).toBe(500);
    });

    it("exposes the expected query keys", () => {
        expect(QUERY_KEYS.USER).toBe("user");
        expect(QUERY_KEYS.USERS).toBe("users");
    });

    it("exposes the expected route paths", () => {
        expect(ROUTES.HOME).toBe("/");
        expect(ROUTES.LOGIN).toBe("/login");
        expect(ROUTES.DASHBOARD).toBe("/dashboard");
    });
});
