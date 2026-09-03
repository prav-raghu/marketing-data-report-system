jest.mock("../../../app/services/api-client", () => ({
    apiClient: { get: jest.fn().mockResolvedValue({ id: 1, title: "t", body: "b" }) },
}));

import { act, render, screen } from "@testing-library/react";
import Home from "@/page";
import { useCounterStore } from "@/store/use-counter-store";

describe("Home", () => {
    afterEach(() => {
        act(() => {
            useCounterStore.getState().reset();
        });
    });

    it("renders the hero heading and a link to the about page", () => {
        render(<Home />);

        expect(screen.getByText("Customer Web Template")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /go to about page/i })).toHaveAttribute("href", "/about");
    });

    it("renders the counter, api test, and tailwind showcase sections", () => {
        render(<Home />);

        expect(screen.getByText(/Zustand Store Test/i)).toBeInTheDocument();
        expect(screen.getByText(/Axios API Test/i)).toBeInTheDocument();
        expect(screen.getByText(/Tailwind CSS Showcase/i)).toBeInTheDocument();
    });
});
