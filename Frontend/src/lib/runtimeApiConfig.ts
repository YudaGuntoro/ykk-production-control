const fallbackApiPort = "5241";
export const runtimeApiBaseUrlStorageKey = "pcms_api_base_url";

function getFallbackBaseUrl() {
  if (typeof window === "undefined") {
    return `http://localhost:${fallbackApiPort}`;
  }

  const host =
    window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1"
      ? "localhost"
      : window.location.hostname;

  return `http://${host}:${fallbackApiPort}`;
}

export function normalizeApiBaseUrl(value?: string) {
  const raw = (value || getFallbackBaseUrl()).split(/\s+#/)[0].trim();
  const configured = raw.replace(/\/+$/, "");

  if (
    typeof window !== "undefined" &&
    !["localhost", "127.0.0.1"].includes(window.location.hostname) &&
    /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/i.test(configured)
  ) {
    return `http://${window.location.hostname}:${fallbackApiPort}`;
  }

  return configured;
}

export function getStoredApiBaseUrl() {
  if (typeof window === "undefined") {
    return "";
  }

  return window.localStorage.getItem(runtimeApiBaseUrlStorageKey) || "";
}

export function setStoredApiBaseUrl(value: string) {
  if (typeof window === "undefined") {
    return "";
  }

  const normalized = normalizeApiBaseUrl(value);
  window.localStorage.setItem(runtimeApiBaseUrlStorageKey, normalized);
  return normalized;
}

export function clearStoredApiBaseUrl() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(runtimeApiBaseUrlStorageKey);
}

export function getRuntimeApiBaseUrl(buildTimeBaseUrl?: string) {
  return normalizeApiBaseUrl(getStoredApiBaseUrl() || buildTimeBaseUrl);
}
