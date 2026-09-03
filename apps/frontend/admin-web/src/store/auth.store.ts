import { create } from "zustand";
import { authTokenStore } from "./auth-token.store";

interface AuthUser {
    id: string;
    username: string;
    email: string;
    role: string;
}

interface AuthState {
    isAuthenticated: boolean;
    user: AuthUser | null;
    setAuth: (user: AuthUser, token: string) => void;
    clearAuth: () => void;
}

export const useAuthStore = create<AuthState>()((set) => ({
    isAuthenticated: false,
    user: null,
    setAuth: (user: AuthUser, token: string) => {
        authTokenStore.setToken(token);
        set({ isAuthenticated: true, user });
    },
    clearAuth: () => {
        authTokenStore.clearToken();
        set({ isAuthenticated: false, user: null });
    },
}));
