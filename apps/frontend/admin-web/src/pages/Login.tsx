import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { z } from "zod";
import { useAuthStore } from "../store/auth.store";
import { apiClient } from "../services/api-client";

const loginSchema = z.object({
    email: z.string().email("Invalid email address"),
    password: z.string().min(1, "Password is required"),
});

type LoginForm = z.infer<typeof loginSchema>;

interface LoginResponse {
    accessToken: string;
    user: {
        id: string;
        username: string;
        email: string;
        role: string;
    };
}

export function Login() {
    const navigate = useNavigate();
    const setAuth = useAuthStore((s) => s.setAuth);
    const [form, setForm] = useState<LoginForm>({ email: "", password: "" });
    const [errors, setErrors] = useState<Partial<LoginForm>>({});
    const [serverError, setServerError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    const validate = (): boolean => {
        const result = loginSchema.safeParse(form);
        if (!result.success) {
            const fieldErrors: Partial<LoginForm> = {};
            result.error.issues.forEach((e) => {
                const field = e.path[0] as keyof LoginForm;
                fieldErrors[field] = e.message;
            });
            setErrors(fieldErrors);
            return false;
        }
        setErrors({});
        return true;
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!validate()) return;

        setLoading(true);
        setServerError(null);
        try {
            const data = await apiClient.post<LoginResponse>("/auth/login", form);
            setAuth(data.user, data.accessToken);
            navigate("/");
        } catch {
            setServerError("Invalid credentials. Please try again.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen bg-background flex items-center justify-center p-4">
            <div className="w-full max-w-md">
                <div className="bg-card border border-border rounded-lg p-8 shadow-sm">
                    <h1 className="text-2xl font-bold text-foreground mb-2">Admin Login</h1>
                    <p className="text-muted-foreground mb-6">Sign in to access the admin dashboard</p>

                    {serverError && (
                        <div className="mb-4 p-3 bg-destructive/10 border border-destructive/20 rounded text-destructive text-sm">
                            {serverError}
                        </div>
                    )}

                    <form onSubmit={handleSubmit} noValidate>
                        <div className="mb-4">
                            <label className="block text-sm font-medium text-foreground mb-1" htmlFor="email">
                                Email
                            </label>
                            <input
                                id="email"
                                type="email"
                                value={form.email}
                                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                                className="w-full px-3 py-2 border border-input rounded-md bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                                autoComplete="email"
                            />
                            {errors.email && <p className="mt-1 text-xs text-destructive">{errors.email}</p>}
                        </div>

                        <div className="mb-6">
                            <label className="block text-sm font-medium text-foreground mb-1" htmlFor="password">
                                Password
                            </label>
                            <input
                                id="password"
                                type="password"
                                value={form.password}
                                onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
                                className="w-full px-3 py-2 border border-input rounded-md bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                                autoComplete="current-password"
                            />
                            {errors.password && <p className="mt-1 text-xs text-destructive">{errors.password}</p>}
                        </div>

                        <button
                            type="submit"
                            disabled={loading}
                            className="w-full py-2 px-4 bg-primary text-primary-foreground font-medium rounded-md hover:opacity-90 transition-opacity disabled:opacity-50"
                        >
                            {loading ? "Signing in..." : "Sign in"}
                        </button>
                    </form>
                </div>
            </div>
        </div>
    );
}
