import { render, screen } from "@testing-library/react";
import { TailwindShowcase } from "@/components/TailwindShowcase";

describe("TailwindShowcase", () => {
    it("renders the showcase heading and feature list", () => {
        render(<TailwindShowcase />);

        expect(screen.getByText(/Tailwind CSS Showcase/i)).toBeInTheDocument();
        expect(screen.getByText(/Gradient backgrounds/i)).toBeInTheDocument();
        expect(screen.getByText(/Animations/i)).toBeInTheDocument();
        expect(screen.getByText(/Responsive design/i)).toBeInTheDocument();
        expect(screen.getByText(/Utility classes/i)).toBeInTheDocument();
    });
});
