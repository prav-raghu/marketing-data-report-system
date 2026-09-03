module.exports = {
    preset: "ts-jest",
    testEnvironment: "node",
    moduleFileExtensions: ["ts", "js", "json"],
    transform: {
        "^.+\\.ts$": ["ts-jest", { tsconfig: "<rootDir>/tsconfig.json" }],
    },
    collectCoverageFrom: ["<rootDir>/src/**/*.ts", "!<rootDir>/src/**/*.d.ts", "!<rootDir>/src/**/index.ts", "!<rootDir>/src/types/**"],
    coveragePathIgnorePatterns: ["/node_modules/", "/dist/", "/tests/"],
    coverageReporters: ["text", "text-summary", "lcov", "html", "json"],
    coverageDirectory: "<rootDir>/coverage",
    coverageThreshold: {
        global: {
            branches: 80,
            functions: 80,
            lines: 80,
            statements: 80,
        },
    },
    testMatch: ["<rootDir>/tests/**/*.spec.ts", "<rootDir>/tests/**/*.test.ts"],
    verbose: true,
    testTimeout: 30000,
    clearMocks: true,
    restoreMocks: true,
};
