import { act, render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { ProtectedRoute } from "../../../src/components/ProtectedRoute";
import { useAuthStore } from "../../../src/store/auth.store";

function renderProtected() {
    return render(
        <MemoryRouter initialEntries={["/dashboard"]}>
            <Routes>
                <Route
                    path="/dashboard"
                    element={
                        <ProtectedRoute>
                            <div>Protected content</div>
                        </ProtectedRoute>
                    }
                />
                <Route path="/login" element={<div>Login page</div>} />
            </Routes>
        </MemoryRouter>,
    );
}

describe("ProtectedRoute", () => {
    afterEach(() => {
        act(() => {
            useAuthStore.getState().clearAuth();
        });
    });

    it("redirects to /login when the user is not authenticated", () => {
        renderProtected();

        expect(screen.getByText("Login page")).toBeInTheDocument();
        expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
    });

    it("renders the protected content when the user is authenticated", () => {
        useAuthStore.getState().setAuth({ id: "u1", username: "admin", email: "admin@test.com", role: "Super Admin" }, "token");

        renderProtected();

        expect(screen.getByText("Protected content")).toBeInTheDocument();
    });
});
