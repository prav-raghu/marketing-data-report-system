import { act, render, screen } from "@testing-library/react";
import AboutPage from "@/about/page";
import { useCounterStore } from "@/store/use-counter-store";

describe("AboutPage", () => {
    afterEach(() => {
        act(() => {
            useCounterStore.getState().reset();
        });
    });

    it("renders the tech stack heading and the current counter value", () => {
        act(() => {
            useCounterStore.getState().setCount(7);
        });
        render(<AboutPage />);

        expect(screen.getByText("About This Template")).toBeInTheDocument();
        expect(screen.getByText("7")).toBeInTheDocument();
    });

    it("links back home and to a 404 test route", () => {
        render(<AboutPage />);

        expect(screen.getByRole("link", { name: /back to home/i })).toHaveAttribute("href", "/");
        expect(screen.getByRole("link", { name: /test 404 page/i })).toHaveAttribute("href", "/nonexistent");
    });
});
