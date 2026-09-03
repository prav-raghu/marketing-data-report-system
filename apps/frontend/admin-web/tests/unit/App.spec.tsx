jest.mock("../../src/services/api-client", () => ({
    apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));

import { act, render, screen } from "@testing-library/react";
import App from "../../src/App";
import { useAuthStore } from "../../src/store/auth.store";

describe("App", () => {
    afterEach(() => {
        act(() => {
            useAuthStore.getState().clearAuth();
        });
        window.history.pushState({}, "", "/");
    });

    it("redirects unauthenticated visitors from / to /login", () => {
        render(<App />);
        expect(screen.getByText("Admin Login")).toBeInTheDocument();
    });

    it("renders the home page for authenticated visitors", () => {
        act(() => {
            useAuthStore.getState().setAuth({ id: "u1", username: "admin", email: "admin@test.com", role: "Super Admin" }, "token");
        });

        render(<App />);

        expect(screen.getByText("Customer Web Template")).toBeInTheDocument();
    });

    it("renders the not-found page for an unknown route", () => {
        window.history.pushState({}, "", "/does-not-exist");
        render(<App />);

        expect(screen.getByText("404")).toBeInTheDocument();
    });
});
