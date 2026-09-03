import type { Config } from "tailwindcss";

export default {
    content: ["index.html", "src/**/*.{ts,tsx,js,jsx}"],
    theme: {
        extend: {
            colors: {
                background: "var(--background)",
                foreground: "var(--foreground)",
                muted: "var(--muted)",
                "muted-foreground": "var(--muted-foreground)",
                popover: "var(--popover)",
                "popover-foreground": "var(--popover-foreground)",
                card: "var(--card)",
                "card-foreground": "var(--card-foreground)",
                border: "var(--border)",
                input: "var(--input)",
                primary: "var(--primary)",
                "primary-foreground": "var(--primary-foreground)",
                secondary: "var(--secondary)",
                "secondary-foreground": "var(--secondary-foreground)",
                accent: "var(--accent)",
                "accent-foreground": "var(--accent-foreground)",
                destructive: "var(--destructive)",
                "destructive-foreground": "var(--destructive-foreground)",
                ring: "var(--ring)",
            },
            borderRadius: {
                DEFAULT: "var(--radius)",
            },
        },
    },
    plugins: [],
} satisfies Config;
