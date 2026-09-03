"use client";

import { apiClient } from "@/services/api-client";
import { useApiStore } from "@/store/use-api-store";
import { useState } from "react";

interface Post {
    userId: number;
    id: number;
    title: string;
    body: string;
}

export function ApiTestCard() {
    const { data, loading, error, setData, setLoading, setError } = useApiStore();
    const [retryCount, setRetryCount] = useState(0);

    const fetchData = async () => {
        setLoading(true);
        setError(null);

        try {
            const response = await apiClient.get<Post>("https://jsonplaceholder.typicode.com/posts/1");
            setData({
                id: response.id,
                title: response.title,
                body: response.body,
            });
            setRetryCount(0);
        } catch (err) {
            if (retryCount < 3) {
                setRetryCount(retryCount + 1);
                setTimeout(() => fetchData(), 1000);
            } else {
                setError(err instanceof Error ? err.message : "Failed to fetch data");
            }
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="bg-white rounded-lg shadow-lg p-6 border-2 border-purple-500">
            <h2 className="text-2xl font-bold text-gray-800 mb-4">🌐 Axios API Test</h2>
            <div className="space-y-4">
                <button
                    onClick={fetchData}
                    disabled={loading}
                    className={`w-full px-4 py-2 text-white rounded transition-colors ${
                        loading ? "bg-gray-400 cursor-not-allowed" : "bg-purple-500 hover:bg-purple-600"
                    }`}
                >
                    {loading ? "Loading..." : "Fetch Data from API"}
                </button>

                {error && (
                    <div className="p-4 bg-red-100 border border-red-400 text-red-700 rounded">
                        <p className="font-semibold">Error:</p>
                        <p className="text-sm">{error}</p>
                        {retryCount > 0 && <p className="text-xs mt-2">Retry attempt: {retryCount}/3</p>}
                    </div>
                )}

                {data && (
                    <div className="p-4 bg-green-50 border border-green-400 rounded">
                        <p className="font-semibold text-green-800">✅ Success! Data fetched:</p>
                        <div className="mt-2 text-sm text-gray-700">
                            <p>
                                <strong>ID:</strong> {data.id}
                            </p>
                            <p>
                                <strong>Title:</strong> {data.title}
                            </p>
                            <p className="mt-2">
                                <strong>Body:</strong> {data.body}
                            </p>
                        </div>
                    </div>
                )}

                <p className="text-sm text-gray-600 text-center">✅ Axios with interceptors & retry logic</p>
            </div>
        </div>
    );
}
