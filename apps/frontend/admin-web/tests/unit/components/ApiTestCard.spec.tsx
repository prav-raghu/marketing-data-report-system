const mockApiClient = { get: jest.fn() };
jest.mock("../../../src/services/api-client", () => ({
    apiClient: mockApiClient,
}));

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ApiTestCard } from "../../../src/components/ApiTestCard";
import { useApiStore } from "../../../src/store/use-api-store";

describe("ApiTestCard", () => {
    afterEach(() => {
        useApiStore.getState().reset();
    });

    it("fetches and displays data on success", async () => {
        mockApiClient.get.mockResolvedValue({ id: 1, title: "Post title", body: "Post body" });
        const user = userEvent.setup();
        render(<ApiTestCard />);

        await user.click(screen.getByRole("button", { name: /fetch data/i }));

        expect(await screen.findByText(/Success! Data fetched/i)).toBeInTheDocument();
        expect(screen.getByText("Post title")).toBeInTheDocument();
    });

    it("schedules a retry after a failed fetch", async () => {
        jest.useFakeTimers();
        mockApiClient.get.mockRejectedValue(new Error("Network error"));
        const user = userEvent.setup({ delay: null });
        render(<ApiTestCard />);

        await user.click(screen.getByRole("button", { name: /fetch data/i }));
        await waitFor(() => expect(mockApiClient.get).toHaveBeenCalledTimes(1));

        jest.advanceTimersByTime(1000);
        await waitFor(() => expect(mockApiClient.get).toHaveBeenCalledTimes(2));

        jest.useRealTimers();
    });
});
