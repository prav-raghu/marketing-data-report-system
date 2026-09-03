export interface User {
    id: string;
    email: string;
    name: string;
    role: string;
    createdAt: Date;
    updatedAt: Date;
}

export interface AuthResponse {
    accessToken: string;
    refreshToken: string;
    user: User;
}
