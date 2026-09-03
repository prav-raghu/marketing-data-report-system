"use client";

import { ApiTestCard } from "@/components/ApiTestCard";
import { CounterCard } from "@/components/CounterCard";
import { TailwindShowcase } from "@/components/TailwindShowcase";
import Link from "next/link";

export default function Home() {
    return (
        <div className="min-h-screen bg-linear-to-br from-gray-50 to-gray-100 py-12 px-4">
            <div className="max-w-6xl mx-auto">
                <header className="text-center mb-12">
                    <h1 className="text-5xl font-bold text-gray-800 mb-4">Customer Web Template</h1>
                    <p className="text-xl text-gray-600">Next.js + TypeScript + Tailwind + Zustand + Axios + React Query</p>
                    <div className="flex justify-center gap-4 mt-6">
                        <Link
                            href="/about"
                            className="px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors shadow-md"
                        >
                            Go to About Page →
                        </Link>
                    </div>
                </header>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
                    <CounterCard />
                    <ApiTestCard />
                </div>

                <div className="mb-6">
                    <TailwindShowcase />
                </div>

                <footer className="text-center mt-12 p-6 bg-white rounded-lg shadow-md">
                    <h3 className="text-lg font-semibold text-gray-800 mb-3">✅ All Features Working:</h3>
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                        <div className="p-3 bg-green-50 rounded border border-green-200">
                            <div className="font-semibold text-green-700">🎨 Tailwind CSS</div>
                            <div className="text-gray-600">Styling ✓</div>
                        </div>
                        <div className="p-3 bg-blue-50 rounded border border-blue-200">
                            <div className="font-semibold text-blue-700">🧭 Next.js Router</div>
                            <div className="text-gray-600">Navigation ✓</div>
                        </div>
                        <div className="p-3 bg-purple-50 rounded border border-purple-200">
                            <div className="font-semibold text-purple-700">📦 Zustand</div>
                            <div className="text-gray-600">State Management ✓</div>
                        </div>
                        <div className="p-3 bg-orange-50 rounded border border-orange-200">
                            <div className="font-semibold text-orange-700">🌐 Axios</div>
                            <div className="text-gray-600">HTTP Client ✓</div>
                        </div>
                    </div>
                </footer>
            </div>
        </div>
    );
}
