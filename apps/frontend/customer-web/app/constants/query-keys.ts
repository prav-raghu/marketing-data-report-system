export const QUERY_KEYS = {
    USER: "user",
    USERS: "users",
    PROFILE: "profile",
} as const;

export type QueryKey = (typeof QUERY_KEYS)[keyof typeof QUERY_KEYS];
