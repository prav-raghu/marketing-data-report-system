import { withSentryConfig } from "@sentry/nextjs";

/** @type {import('next').NextConfig} */
const nextConfig = {
    reactStrictMode: true,
    poweredByHeader: false,
    output: "standalone",

    experimental: {
        optimizePackageImports: ["@tanstack/react-query"],
    },

    async rewrites() {
        return [
            {
                source: "/api/:path*",
                destination: "http://localhost:4002/:path*",
            },
        ];
    },

    images: {
        remotePatterns: [
            {
                protocol: "https",
                hostname: "example.com",
            },
        ],
    },
};

export default withSentryConfig(nextConfig, {
    org: process.env.SENTRY_ORG,
    project: process.env.SENTRY_PROJECT,
    silent: true,
});
