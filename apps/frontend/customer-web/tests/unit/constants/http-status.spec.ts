import { HTTP_STATUS } from "@/constants/http-status";

describe("HTTP_STATUS", () => {
    it("exposes the expected status codes", () => {
        expect(HTTP_STATUS.OK).toBe(200);
        expect(HTTP_STATUS.CREATED).toBe(201);
        expect(HTTP_STATUS.BAD_REQUEST).toBe(400);
        expect(HTTP_STATUS.UNAUTHORIZED).toBe(401);
        expect(HTTP_STATUS.FORBIDDEN).toBe(403);
        expect(HTTP_STATUS.NOT_FOUND).toBe(404);
        expect(HTTP_STATUS.INTERNAL_SERVER_ERROR).toBe(500);
    });
});
