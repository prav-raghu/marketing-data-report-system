jest.mock("../../../src/services/api-client", () => ({
    apiClient: { get: jest.fn(), post: jest.fn(), put: jest.fn(), delete: jest.fn() },
}));

import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { Home } from "../../../src/pages/Home";

describe("Home", () => {
    it("renders the landing page heading and feature cards", () => {
        render(
            <MemoryRouter>
                <Home />
            </MemoryRouter>,
        );

        expect(screen.getByText("Customer Web Template")).toBeInTheDocument();
        expect(screen.getByText(/Zustand Store Test/i)).toBeInTheDocument();
        expect(screen.getByText(/Axios API Test/i)).toBeInTheDocument();
        expect(screen.getByText(/Tailwind CSS Showcase/i)).toBeInTheDocument();
    });
});
