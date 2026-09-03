import { render, screen } from "@testing-library/react";
import NotFound from "@/not-found";

describe("NotFound", () => {
    it("renders the 404 message and a link back home", () => {
        render(<NotFound />);

        expect(screen.getByText("404")).toBeInTheDocument();
        expect(screen.getByText(/Page Not Found/i)).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /back to home/i })).toHaveAttribute("href", "/");
    });
});
