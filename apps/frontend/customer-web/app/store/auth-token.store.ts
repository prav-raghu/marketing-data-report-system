let accessToken: string | null = null;

export const authTokenStore = {
    getToken: (): string | null => accessToken,
    setToken: (token: string): void => {
        accessToken = token;
    },
    clearToken: (): void => {
        accessToken = null;
    },
};
