import type { ApiResponse } from "./types";
import { clearAuthSession, getStoredToken, redirectToSignIn } from "./auth";
import { getRuntimeApiBaseUrl } from "./runtimeApiConfig";

export function getApiBaseUrl() {
  return getRuntimeApiBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL);
}

function parseApiResponse<T>(text: string): ApiResponse<T> | null {
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text) as ApiResponse<T>;
  } catch {
    return {
      success: false,
      statusCode: 0,
      message: text.slice(0, 300) || "Server returned an invalid JSON response.",
      data: undefined as T,
    };
  }
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const token = getStoredToken();
  const isFormData = typeof FormData !== "undefined" && init.body instanceof FormData;

  if (!headers.has("Content-Type") && init.body && !isFormData) {
    headers.set("Content-Type", "application/json");
  }

  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers,
  });
  const text = await response.text();
  const payload = parseApiResponse<T>(text);

  if (response.status === 401) {
    clearAuthSession();
    if (typeof window !== "undefined" && !window.location.pathname.startsWith("/signin")) {
      redirectToSignIn();
    }
  }

  if (!response.ok || payload?.success === false) {
    throw new Error(payload?.message || `Request failed with status ${response.status}`);
  }

  return payload ? payload.data : (undefined as T);
}

export function apiGet<T>(path: string) {
  return apiRequest<T>(path);
}

export function apiPost<T>(path: string, body?: unknown) {
  return apiRequest<T>(path, {
    method: "POST",
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export function apiPut<T>(path: string, body?: unknown) {
  return apiRequest<T>(path, {
    method: "PUT",
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}
