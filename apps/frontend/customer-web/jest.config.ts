import type { Config } from "jest";
import nextJest from "next/jest.js";

const createJestConfig = nextJest({ dir: "./" });

const customJestConfig: Config = {
    testEnvironment: "jsdom",
    setupFilesAfterEnv: ["<rootDir>/tests/setup.ts"],
    testMatch: ["<rootDir>/tests/**/*.spec.ts", "<rootDir>/tests/**/*.spec.tsx"],
    modulePathIgnorePatterns: ["<rootDir>/.next/"],
    collectCoverageFrom: ["app/**/*.{ts,tsx}", "!app/**/*.d.ts", "!app/layout.tsx", "!app/global-error.tsx", "!app/**/layout.tsx"],
    coverageReporters: ["text", "text-summary", "lcov", "html", "json"],
    coverageThreshold: {
        global: {
            branches: 80,
            functions: 80,
            lines: 80,
            statements: 80,
        },
    },
    verbose: true,
    clearMocks: true,
    restoreMocks: true,
};

export default createJestConfig(customJestConfig);
