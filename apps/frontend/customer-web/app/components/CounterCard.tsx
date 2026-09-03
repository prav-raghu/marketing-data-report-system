"use client";

import { useCounterStore } from "@/store/use-counter-store";

export function CounterCard() {
    const { count, increment, decrement, reset } = useCounterStore();

    return (
        <div className="bg-white rounded-lg shadow-lg p-6 border-2 border-blue-500">
            <h2 className="text-2xl font-bold text-gray-800 mb-4">🔢 Zustand Store Test</h2>
            <div className="flex flex-col items-center space-y-4">
                <div className="text-5xl font-bold text-blue-600">{count}</div>
                <div className="flex gap-2">
                    <button onClick={decrement} className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 transition-colors">
                        - Decrement
                    </button>
                    <button onClick={reset} className="px-4 py-2 bg-gray-500 text-white rounded hover:bg-gray-600 transition-colors">
                        Reset
                    </button>
                    <button onClick={increment} className="px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600 transition-colors">
                        + Increment
                    </button>
                </div>
                <p className="text-sm text-gray-600">✅ Zustand state persisted to localStorage</p>
            </div>
        </div>
    );
}
