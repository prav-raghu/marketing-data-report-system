const mockAxiosInstance = {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
    request: jest.fn(),
    interceptors: {
        request: { use: jest.fn() },
        response: { use: jest.fn() },
    },
};

jest.mock("axios", () => ({
    __esModule: true,
    default: {
        create: jest.fn(() => mockAxiosInstance),
    },
}));

import { authTokenStore } from "../../../src/store/auth-token.store";
import { apiClient } from "../../../src/services/api-client";

describe("apiClient", () => {
    let requestInterceptor: (config: never) => never;
    let requestErrorInterceptor: (error: Error) => never;
    let responseSuccessInterceptor: (response: never) => never;
    let responseErrorInterceptor: (error: unknown) => Promise<unknown>;

    beforeAll(() => {
        [requestInterceptor, requestErrorInterceptor] = mockAxiosInstance.interceptors.request.use.mock.calls[0];
        [responseSuccessInterceptor, responseErrorInterceptor] = mockAxiosInstance.interceptors.response.use.mock.calls[0];
    });

    beforeEach(() => {
        authTokenStore.clearToken();
    });

    describe("request interceptor", () => {
        it("attaches the bearer token when one is present", () => {
            authTokenStore.setToken("token-123");
            const config = { headers: {} as Record<string, string> };

            const result = requestInterceptor(config as never);

            expect((result as typeof config).headers.Authorization).toBe("Bearer token-123");
        });

        it("leaves headers untouched when there is no token", () => {
            const config = { headers: {} as Record<string, string> };

            const result = requestInterceptor(config as never);

            expect((result as typeof config).headers.Authorization).toBeUndefined();
        });

        it("rethrows request errors", () => {
            expect(() => requestErrorInterceptor(new Error("boom"))).toThrow("boom");
        });
    });

    describe("response interceptor", () => {
        it("passes successful responses through unchanged", () => {
            const response = { data: { ok: true } };
            expect(responseSuccessInterceptor(response as never)).toBe(response);
        });

        it("clears the token and rethrows on a 401", async () => {
            authTokenStore.setToken("token-123");
            const error = { response: { status: 401 }, config: {} };

            await expect(responseErrorInterceptor(error)).rejects.toBe(error);
            expect(authTokenStore.getToken()).toBeNull();
        });

        it("rethrows immediately when there is no request config", async () => {
            const error = { response: { status: 500 }, config: undefined };

            await expect(responseErrorInterceptor(error)).rejects.toBe(error);
        });

        it("retries retryable errors up to the max retry count", async () => {
            jest.useFakeTimers();
            mockAxiosInstance.request.mockResolvedValue({ data: "recovered" });
            const error = { response: { status: 503 }, config: { _retryCount: 0 } };

            const promise = responseErrorInterceptor(error);
            await jest.advanceTimersByTimeAsync(1000);

            await expect(promise).resolves.toEqual({ data: "recovered" });
            expect(mockAxiosInstance.request).toHaveBeenCalledWith(expect.objectContaining({ _retryCount: 1 }));
            jest.useRealTimers();
        });

        it("gives up and rethrows once the retry limit is exceeded", async () => {
            const error = { response: { status: 503 }, config: { _retryCount: 3 } };

            await expect(responseErrorInterceptor(error)).rejects.toBe(error);
            expect(mockAxiosInstance.request).not.toHaveBeenCalled();
        });

        it("does not retry non-retryable client errors", async () => {
            const error = { response: { status: 400 }, config: { _retryCount: 0 } };

            await expect(responseErrorInterceptor(error)).rejects.toBe(error);
            expect(mockAxiosInstance.request).not.toHaveBeenCalled();
        });
    });

    describe("HTTP methods", () => {
        it("get returns the response data", async () => {
            mockAxiosInstance.get.mockResolvedValue({ data: { id: 1 } });

            await expect(apiClient.get("/things")).resolves.toEqual({ id: 1 });
            expect(mockAxiosInstance.get).toHaveBeenCalledWith("/things");
        });

        it("post returns the response data", async () => {
            mockAxiosInstance.post.mockResolvedValue({ data: { created: true } });

            await expect(apiClient.post("/things", { name: "a" })).resolves.toEqual({ created: true });
            expect(mockAxiosInstance.post).toHaveBeenCalledWith("/things", { name: "a" });
        });

        it("put returns the response data", async () => {
            mockAxiosInstance.put.mockResolvedValue({ data: { updated: true } });

            await expect(apiClient.put("/things/1", { name: "b" })).resolves.toEqual({ updated: true });
        });

        it("delete returns the response data", async () => {
            mockAxiosInstance.delete.mockResolvedValue({ data: { deleted: true } });

            await expect(apiClient.delete("/things/1")).resolves.toEqual({ deleted: true });
        });

        it("getAxiosInstance returns the underlying axios instance", () => {
            expect(apiClient.getAxiosInstance()).toBe(mockAxiosInstance);
        });
    });
});
