import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig([
    globalIgnores([
        "dist",
        ".next",
        "next.config.ts",
        "next-env.d.ts",
        "instrumentation.ts",
        "tailwind.config.ts",
        "sentry.client.config.ts",
        "sentry.edge.config.ts",
        "sentry.server.config.ts",
    ]),
    {
        files: ["**/*.{ts,tsx}"],
        extends: [js.configs.recommended, tseslint.configs.recommended, reactRefresh.configs.vite],
        plugins: {
            "react-hooks": reactHooks,
        },
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
            parserOptions: {
                tsconfigRootDir: __dirname,
                project: ["./tsconfig.json"],
            },
        },
        rules: {
            ...reactHooks.configs["recommended-latest"].rules,
            "react-refresh/only-export-components": [
                "warn",
                {
                    allowConstantExport: true,
                    allowExportNames: [
                        "metadata",
                        "viewport",
                        "generateMetadata",
                        "generateViewport",
                        "generateStaticParams",
                        "dynamic",
                        "dynamicParams",
                        "revalidate",
                        "fetchCache",
                        "runtime",
                        "preferredRegion",
                        "maxDuration",
                    ],
                },
            ],
        },
    },
]);
