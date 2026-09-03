import { useState, useEffect } from "react";

type Theme = "light" | "dark" | "system";

export function useTheme() {
    const [theme, setTheme] = useState<Theme>(() => {
        const stored = localStorage.getItem("theme") as Theme;
        return stored || "system";
    });

    useEffect(() => {
        const root = document.documentElement;

        const applyTheme = (selectedTheme: Theme) => {
            if (selectedTheme === "system") {
                const systemTheme = globalThis.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
                root.dataset.theme = systemTheme;
            } else {
                root.dataset.theme = selectedTheme;
            }
        };

        applyTheme(theme);
        localStorage.setItem("theme", theme);

        if (theme === "system") {
            const mediaQuery = globalThis.matchMedia("(prefers-color-scheme: dark)");
            const handler = () => applyTheme("system");
            mediaQuery.addEventListener("change", handler);
            return () => mediaQuery.removeEventListener("change", handler);
        }
    }, [theme]);

    return { theme, setTheme };
}
