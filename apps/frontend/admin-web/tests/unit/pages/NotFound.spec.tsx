import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { NotFound } from "../../../src/pages/NotFound";

describe("NotFound", () => {
    it("renders a 404 message with a link back home", () => {
        render(
            <MemoryRouter>
                <NotFound />
            </MemoryRouter>,
        );

        expect(screen.getByText("404")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /back to home/i })).toHaveAttribute("href", "/");
    });
});
