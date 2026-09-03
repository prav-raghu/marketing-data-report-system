import { ROUTES } from "@/constants/routes";

describe("ROUTES", () => {
    it("exposes the expected route paths", () => {
        expect(ROUTES.HOME).toBe("/");
        expect(ROUTES.LOGIN).toBe("/login");
        expect(ROUTES.REGISTER).toBe("/register");
        expect(ROUTES.DASHBOARD).toBe("/dashboard");
        expect(ROUTES.PROFILE).toBe("/profile");
    });
});
