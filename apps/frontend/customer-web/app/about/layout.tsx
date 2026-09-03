import type { Metadata } from "next";

export const metadata: Metadata = {
    title: "About Us",
    description: "Learn more about our company, mission, and values. This template demonstrates production-ready Next.js development.",
    keywords: ["about", "company", "mission", "values", "team", "next.js template"],
    openGraph: {
        title: "About Us | Customer Web App",
        description: "Learn more about our company, mission, and values.",
        url: "/about",
        images: [
            {
                url: "/og-about.jpg",
                width: 1200,
                height: 630,
                alt: "About Us",
            },
        ],
    },
};

export default function AboutLayout({ children }: { children: React.ReactNode }) {
    return children;
}
