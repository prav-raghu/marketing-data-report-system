import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CounterCard } from "../../../src/components/CounterCard";
import { useCounterStore } from "../../../src/store/use-counter-store";

describe("CounterCard", () => {
    afterEach(() => {
        act(() => {
            useCounterStore.getState().reset();
        });
    });

    it("renders the current count", () => {
        render(<CounterCard />);
        expect(screen.getByText("0")).toBeInTheDocument();
    });

    it("increments the count when clicking increment", async () => {
        const user = userEvent.setup();
        render(<CounterCard />);

        await user.click(screen.getByRole("button", { name: /increment/i }));

        expect(screen.getByText("1")).toBeInTheDocument();
    });

    it("decrements the count when clicking decrement", async () => {
        const user = userEvent.setup();
        useCounterStore.getState().setCount(5);
        render(<CounterCard />);

        await user.click(screen.getByRole("button", { name: /decrement/i }));

        expect(screen.getByText("4")).toBeInTheDocument();
    });

    it("resets the count when clicking reset", async () => {
        const user = userEvent.setup();
        useCounterStore.getState().setCount(5);
        render(<CounterCard />);

        await user.click(screen.getByRole("button", { name: /reset/i }));

        expect(screen.getByText("0")).toBeInTheDocument();
    });
});
