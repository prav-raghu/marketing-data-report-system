import axios, { type AxiosInstance, type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { authTokenStore } from "../store/auth-token.store";

const API_BASE_URL = process.env.NEXT_PUBLIC_CUSTOMER_API_BASE_URL || "http://localhost:3001";
const MAX_RETRIES = 3;
const RETRY_DELAY_BASE = 1000;

interface RetryConfig extends InternalAxiosRequestConfig {
    _retryCount?: number;
}

class ApiClient {
    private readonly client: AxiosInstance;

    constructor() {
        this.client = axios.create({
            baseURL: API_BASE_URL,
            timeout: 10000,
            headers: {
                "Content-Type": "application/json",
            },
        });

        this.setupInterceptors();
    }

    private isRetryableError(error: AxiosError): boolean {
        if (!error.response) {
            return true;
        }
        const status = error.response.status;
        return status === 408 || status === 429 || status >= 500;
    }

    private async delay(ms: number): Promise<void> {
        return new Promise((resolve) => setTimeout(resolve, ms));
    }

    private setupInterceptors(): void {
        this.client.interceptors.request.use(
            (config) => {
                const token = authTokenStore.getToken();
                if (token) {
                    config.headers.Authorization = `Bearer ${token}`;
                }
                return config;
            },
            (error: Error) => {
                throw error;
            },
        );

        this.client.interceptors.response.use(
            (response) => response,
            async (error: AxiosError) => {
                const config = error.config as RetryConfig;

                if (!config) {
                    throw error;
                }

                config._retryCount = config._retryCount ?? 0;

                if (error.response?.status === 401) {
                    authTokenStore.clearToken();
                    throw error;
                }

                if (config._retryCount < MAX_RETRIES && this.isRetryableError(error)) {
                    config._retryCount += 1;
                    const delayMs = RETRY_DELAY_BASE * Math.pow(2, config._retryCount - 1);
                    await this.delay(delayMs);
                    return this.client.request(config);
                }

                throw error;
            },
        );
    }

    public async get<T>(url: string): Promise<T> {
        const response = await this.client.get<T>(url);
        return response.data;
    }

    public async post<T>(url: string, data?: unknown): Promise<T> {
        const response = await this.client.post<T>(url, data);
        return response.data;
    }

    public async put<T>(url: string, data?: unknown): Promise<T> {
        const response = await this.client.put<T>(url, data);
        return response.data;
    }

    public async delete<T>(url: string): Promise<T> {
        const response = await this.client.delete<T>(url);
        return response.data;
    }

    public getAxiosInstance(): AxiosInstance {
        return this.client;
    }
}

export const apiClient = new ApiClient();
