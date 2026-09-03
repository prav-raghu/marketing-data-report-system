import { act, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { About } from "../../../src/pages/About";
import { useCounterStore } from "../../../src/store/use-counter-store";

describe("About", () => {
    afterEach(() => {
        act(() => {
            useCounterStore.getState().reset();
        });
    });

    it("renders the page heading and the persisted counter value", () => {
        useCounterStore.getState().setCount(7);

        render(
            <MemoryRouter>
                <About />
            </MemoryRouter>,
        );

        expect(screen.getByText("About This Template")).toBeInTheDocument();
        expect(screen.getByText("7")).toBeInTheDocument();
    });
});
