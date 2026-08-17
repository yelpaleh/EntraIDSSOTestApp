window.__APP_CONFIG__ = {
  VITE_API_BASE_URL: "https://localhost:7575"
};
=======
export interface AppConfig {
    API_BASE_URL: string;
    APP_ENVIRONMENT: string;
    ENABLE_MOCK_API: boolean;
}

declare global {
    interface Window {
        __APP_CONFIG__?: Partial<AppConfig>;
    }
}

const config: AppConfig = {
    API_BASE_URL: "",
    APP_ENVIRONMENT: "UNKNOWN",
    ENABLE_MOCK_API: false,

    ...(window.__APP_CONFIG__ || {}),
};

export function getConfig(): AppConfig {
    return config;
}

export function getApiBaseUrl(): string {
    const url = config.API_BASE_URL?.trim();

    if (!url) {
        throw new Error(
            "API_BASE_URL is not configured."
        );
    }

    return url.replace(/\/+$/, "");
}

export default config;