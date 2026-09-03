import { create } from "zustand";
import { devtools } from "zustand/middleware";

interface ApiData {
    id: number;
    title: string;
    body: string;
}

interface ApiState {
    data: ApiData | null;
    loading: boolean;
    error: string | null;
    setData: (data: ApiData | null) => void;
    setLoading: (loading: boolean) => void;
    setError: (error: string | null) => void;
    reset: () => void;
}

export const useApiStore = create<ApiState>()(
    devtools((set) => ({
        data: null,
        loading: false,
        error: null,
        setData: (data) => set({ data, error: null }),
        setLoading: (loading) => set({ loading }),
        setError: (error) => set({ error, loading: false }),
        reset: () => set({ data: null, loading: false, error: null }),
    })),
);
