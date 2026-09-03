import type { Config } from "jest";

const config: Config = {
    preset: "ts-jest",
    testEnvironment: "jsdom",
    moduleFileExtensions: ["ts", "tsx", "js", "jsx", "json"],
    transform: {
        "^.+\\.(ts|tsx)$": "<rootDir>/tests/import-meta-transform.cjs",
    },
    moduleNameMapper: {
        "\\.(css|less|scss|sass)$": "identity-obj-proxy",
    },
    setupFilesAfterEnv: ["<rootDir>/tests/setup.ts"],
    coverageDirectory: "coverage",
    collectCoverageFrom: ["src/**/*.{ts,tsx}", "!src/**/*.d.ts", "!src/main.tsx", "!src/vite-env.d.ts"],
    coverageReporters: ["text", "text-summary", "lcov", "html", "json"],
    coverageThreshold: {
        global: {
            branches: 80,
            functions: 80,
            lines: 80,
            statements: 80,
        },
    },
    testMatch: ["<rootDir>/tests/**/*.spec.ts", "<rootDir>/tests/**/*.spec.tsx"],
    verbose: true,
    clearMocks: true,
    restoreMocks: true,
};

export default config;
