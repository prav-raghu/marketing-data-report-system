import "./globals.css";
import type { Metadata } from "next";

export const metadata: Metadata = {
    title: {
        default: "Customer Web App",
        template: "%s | Customer Web App",
    },
    description: "A production-ready customer-facing web application built with Next.js, TypeScript, and Tailwind CSS.",
    keywords: ["next.js", "react", "typescript", "web application", "customer portal"],
    authors: [{ name: "Your Company Name" }],
    creator: "Your Company Name",
    publisher: "Your Company Name",
    robots: {
        index: true,
        follow: true,
    },
    openGraph: {
        type: "website",
        locale: "en_US",
        url: "https://example.com",
        siteName: "Customer Web App",
        title: "Customer Web App",
        description: "A production-ready customer-facing web application built with Next.js, TypeScript, and Tailwind CSS.",
        images: [
            {
                url: "https://example.com/og-image.jpg",
                width: 1200,
                height: 630,
                alt: "Customer Web App",
            },
        ],
    },
    twitter: {
        card: "summary_large_image",
        site: "@example",
        creator: "@example",
        title: "Customer Web App",
        description: "A production-ready customer-facing web application built with Next.js, TypeScript, and Tailwind CSS.",
        images: ["https://example.com/og-image.jpg"],
    },
    icons: {
        icon: "/favicon.ico",
        shortcut: "/favicon-16x16.png",
        apple: "/apple-touch-icon.png",
    },
    manifest: "/site.webmanifest",
    metadataBase: new URL("https://example.com"),
};

export default function RootLayout({
    children,
}: Readonly<{
    children: React.ReactNode;
}>) {
    return (
        <html lang="en">
            <body>{children}</body>
        </html>
    );
}
