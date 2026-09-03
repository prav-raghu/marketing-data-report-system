export const QUERY_KEYS = {
    USER: "user",
    USERS: "users",
    PROFILE: "profile",
    SYSTEM_STATS: "system-stats",
} as const;

export type QueryKey = (typeof QUERY_KEYS)[keyof typeof QUERY_KEYS];
