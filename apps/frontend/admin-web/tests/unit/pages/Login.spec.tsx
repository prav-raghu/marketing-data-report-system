const mockNavigate = jest.fn();

jest.mock("react-router-dom", () => ({
    useNavigate: () => mockNavigate,
}));

const mockApiClient = { post: jest.fn() };
jest.mock("../../../src/services/api-client", () => ({
    apiClient: mockApiClient,
}));

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Login } from "../../../src/pages/Login";
import { useAuthStore } from "../../../src/store/auth.store";

describe("Login", () => {
    afterEach(() => {
        useAuthStore.getState().clearAuth();
    });

    it("shows validation errors when submitted empty", async () => {
        const user = userEvent.setup();
        render(<Login />);

        await user.click(screen.getByRole("button", { name: /sign in/i }));

        expect(await screen.findByText("Invalid email address")).toBeInTheDocument();
        expect(screen.getByText("Password is required")).toBeInTheDocument();
        expect(mockApiClient.post).not.toHaveBeenCalled();
    });

    it("shows a validation error for a malformed email", async () => {
        const user = userEvent.setup();
        render(<Login />);

        await user.type(screen.getByLabelText(/email/i), "not-an-email");
        await user.type(screen.getByLabelText(/password/i), "secret");
        await user.click(screen.getByRole("button", { name: /sign in/i }));

        expect(await screen.findByText("Invalid email address")).toBeInTheDocument();
    });

    it("logs in successfully and navigates home", async () => {
        mockApiClient.post.mockResolvedValue({
            accessToken: "token-123",
            user: { id: "u1", username: "admin", email: "admin@test.com", role: "Super Admin" },
        });
        const user = userEvent.setup();
        render(<Login />);

        await user.type(screen.getByLabelText(/email/i), "admin@test.com");
        await user.type(screen.getByLabelText(/password/i), "secret");
        await user.click(screen.getByRole("button", { name: /sign in/i }));

        await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith("/"));
        expect(useAuthStore.getState().isAuthenticated).toBe(true);
    });

    it("shows a toast-style server error banner on invalid credentials", async () => {
        mockApiClient.post.mockRejectedValue(new Error("Unauthorized"));
        const user = userEvent.setup();
        render(<Login />);

        await user.type(screen.getByLabelText(/email/i), "admin@test.com");
        await user.type(screen.getByLabelText(/password/i), "wrong");
        await user.click(screen.getByRole("button", { name: /sign in/i }));

        expect(await screen.findByText("Invalid credentials. Please try again.")).toBeInTheDocument();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
    });
});
