import { render, screen } from "@testing-library/react";
import { TailwindShowcase } from "../../../src/components/TailwindShowcase";

describe("TailwindShowcase", () => {
    it("renders the showcase heading", () => {
        render(<TailwindShowcase />);
        expect(screen.getByText(/Tailwind CSS Showcase/i)).toBeInTheDocument();
    });
});
