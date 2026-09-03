import { formatDate, formatDateTime, formatTime } from "../../../src/utils/formatters";

describe("formatters", () => {
    describe("formatDate", () => {
        it("formats a Date instance as a long-form date", () => {
            expect(formatDate(new Date("2025-03-15T00:00:00Z"))).toBe("March 15, 2025");
        });

        it("formats an ISO date string", () => {
            expect(formatDate("2025-01-01T00:00:00Z")).toBe("January 1, 2025");
        });
    });

    describe("formatDateTime", () => {
        it("formats a date and time together", () => {
            const result = formatDateTime(new Date("2025-03-15T14:30:00Z"));
            expect(result).toEqual(expect.stringContaining("2025"));
            expect(result).toEqual(expect.stringContaining("Mar"));
        });

        it("accepts an ISO date string", () => {
            const result = formatDateTime("2025-03-15T14:30:00Z");
            expect(result).toEqual(expect.stringContaining("2025"));
        });
    });

    describe("formatTime", () => {
        it("formats only the time portion", () => {
            const result = formatTime(new Date("2025-03-15T14:30:00Z"));
            expect(result).not.toEqual(expect.stringContaining("2025"));
        });

        it("accepts an ISO date string", () => {
            const result = formatTime("2025-03-15T14:30:00Z");
            expect(result).not.toEqual(expect.stringContaining("2025"));
        });
    });
});
