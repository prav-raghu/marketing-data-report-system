export default {
    "*.{ts,tsx}": ["pnpm exec prettier --write"],
    "*.{js,jsx,cjs,mjs}": ["pnpm exec prettier --write"],
    "*.{json,yml,yaml}": ["pnpm exec prettier --write"],
    "*.md": ["pnpm exec prettier --write"],
    "*.prisma": ["pnpm exec prettier --write"],
};
