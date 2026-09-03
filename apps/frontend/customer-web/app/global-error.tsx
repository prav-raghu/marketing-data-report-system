"use client";

export default function GlobalError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
    return (
        <html lang="en">
            <body>
                <div
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "center",
                        justifyContent: "center",
                        minHeight: "100vh",
                        fontFamily: "system-ui, sans-serif",
                        backgroundColor: "#f9fafb",
                        padding: "1rem",
                    }}
                >
                    <div
                        style={{
                            maxWidth: "480px",
                            width: "100%",
                            background: "#fff",
                            borderRadius: "8px",
                            boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                            padding: "2rem",
                            textAlign: "center",
                        }}
                    >
                        <h1
                            style={{
                                fontSize: "1.5rem",
                                fontWeight: 700,
                                color: "#111827",
                                marginBottom: "0.75rem",
                            }}
                        >
                            Something went wrong
                        </h1>
                        {error?.digest && (
                            <p
                                style={{
                                    fontSize: "0.875rem",
                                    color: "#6b7280",
                                    marginBottom: "1rem",
                                }}
                            >
                                Error ID: {error.digest}
                            </p>
                        )}
                        <button
                            onClick={reset}
                            style={{
                                padding: "0.5rem 1.5rem",
                                backgroundColor: "#2563eb",
                                color: "#fff",
                                border: "none",
                                borderRadius: "6px",
                                cursor: "pointer",
                                fontSize: "0.875rem",
                                fontWeight: 500,
                            }}
                        >
                            Try again
                        </button>
                    </div>
                </div>
            </body>
        </html>
    );
}
