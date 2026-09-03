"use client";

import { useCounterStore } from "@/store/use-counter-store";
import Link from "next/link";

export default function AboutPage() {
    const { count } = useCounterStore();

    return (
        <div className="min-h-screen bg-linear-to-br from-blue-50 to-indigo-100 py-12 px-4">
            <div className="max-w-4xl mx-auto">
                <div className="bg-white rounded-lg shadow-xl p-8">
                    <h1 className="text-4xl font-bold text-gray-800 mb-6">About This Template</h1>

                    <div className="prose max-w-none">
                        <p className="text-lg text-gray-700 mb-4">
                            This is a production-ready Next.js template for the customer-facing web application.
                        </p>

                        <h2 className="text-2xl font-semibold text-gray-800 mt-6 mb-3">🚀 Tech Stack</h2>
                        <ul className="space-y-2 text-gray-700">
                            <li className="flex items-center gap-2">
                                <span className="text-blue-500">⚛️</span>
                                <strong>Next.js 15+</strong> - React framework with SSR and SEO
                            </li>
                            <li className="flex items-center gap-2">
                                <span className="text-blue-500">📘</span>
                                <strong>TypeScript</strong> - Type-safe development
                            </li>
                            <li className="flex items-center gap-2">
                                <span className="text-cyan-500">🎨</span>
                                <strong>Tailwind CSS 4</strong> - Utility-first styling
                            </li>
                            <li className="flex items-center gap-2">
                                <span className="text-purple-500">📦</span>
                                <strong>Zustand</strong> - Lightweight state management
                            </li>
                            <li className="flex items-center gap-2">
                                <span className="text-orange-500">🌐</span>
                                <strong>Axios</strong> - HTTP client with interceptors
                            </li>
                            <li className="flex items-center gap-2">
                                <span className="text-pink-500">🧭</span>
                                <strong>Next.js App Router</strong> - File-based routing
                            </li>
                        </ul>

                        <h2 className="text-2xl font-semibold text-gray-800 mt-6 mb-3">🧪 Router Test</h2>
                        <p className="text-gray-700 mb-4">
                            You're currently on the <strong>About</strong> page. The Next.js router is working correctly!
                        </p>
                        <p className="text-gray-700 mb-4">
                            The counter from the Home page persists: <strong className="text-blue-600 text-xl">{count}</strong>
                        </p>
                        <p className="text-sm text-gray-600 italic">
                            ✅ This demonstrates that Zustand store persists across route changes.
                        </p>

                        <div className="mt-8 flex gap-4">
                            <Link
                                href="/"
                                className="px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors shadow-md"
                            >
                                ← Back to Home
                            </Link>
                            <Link
                                href="/nonexistent"
                                className="px-6 py-3 bg-gray-600 text-white rounded-lg hover:bg-gray-700 transition-colors shadow-md"
                            >
                                Test 404 Page
                            </Link>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
