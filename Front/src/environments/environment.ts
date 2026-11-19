declare global {
  interface Window {
    __APP_CONFIG__?: { apiUrl?: string; showDataAssistant?: boolean };
  }
}

export const environment = {
  production: true,
  // Read at access time so the config.json fetched before bootstrap wins.
  get apiUrl(): string {
    const runtime = typeof window !== 'undefined' ? window.__APP_CONFIG__?.apiUrl : undefined;
    return runtime ?? 'https://localhost:9060/api';
  },
  // Feature flag for the "تحدث مع البيانات" assistant; defaults on when unset in config.json.
  get showDataAssistant(): boolean {
    const runtime = typeof window !== 'undefined' ? window.__APP_CONFIG__?.showDataAssistant : undefined;
    return runtime ?? true;
  }
};
