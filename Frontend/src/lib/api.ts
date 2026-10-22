import type { ApiResponse } from "./types";
import { clearAuthSession, getStoredRefreshToken, getStoredToken, redirectToSignIn, saveAuthSession } from "./auth";
import { getRuntimeApiBaseUrl } from "./runtimeApiConfig";
import type { LoginResponse } from "./types";

let refreshInFlight: Promise<LoginResponse | null> | null = null;

async function refreshSession() {
  const refreshToken = getStoredRefreshToken();
  if (!refreshToken) return null;
  if (!refreshInFlight) {
    refreshInFlight = fetch(`${getApiBaseUrl()}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ refresh_token: refreshToken }),
    }).then(async (response) => {
      if (!response.ok) return null;
      const payload = parseApiResponse<LoginResponse>(await response.text());
      if (!payload?.success || !payload.data?.access_token) return null;
      saveAuthSession(payload.data);
      return payload.data;
    }).catch(() => null).finally(() => { refreshInFlight = null; });
  }
  return refreshInFlight;
}

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

  const requestUrl = `${getApiBaseUrl()}${path}`;
  const response = await fetch(requestUrl, {
    ...init,
    headers,
  });
  let finalResponse = response;
  let text = await response.text();
  let payload = parseApiResponse<T>(text);

  if (response.status === 401 && path !== "/api/auth/refresh" && getStoredRefreshToken()) {
    const refreshed = await refreshSession();
    if (refreshed) {
      const retryHeaders = new Headers(headers);
      retryHeaders.set("Authorization", `Bearer ${refreshed.access_token}`);
      finalResponse = await fetch(requestUrl, { ...init, headers: retryHeaders });
      text = await finalResponse.text();
      payload = parseApiResponse<T>(text);
    }
  }

  if (finalResponse.status === 401) {
    clearAuthSession();
    if (typeof window !== "undefined" && !window.location.pathname.startsWith("/signin")) redirectToSignIn();
  }

  if (!finalResponse.ok || payload?.success === false) {
    throw new Error(payload?.message || `Request failed with status ${finalResponse.status}`);
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
