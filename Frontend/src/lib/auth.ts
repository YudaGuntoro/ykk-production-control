import type { LoginResponse, UserResponse } from "./types";

const tokenKey = "pcms_access_token";
const userKey = "pcms_user";
const expiresKey = "pcms_expires_at";
const accessiblePagesKey = "pcms_accessible_pages";

function readStorage(key: string) {
  if (typeof window === "undefined") {
    return null;
  }

  return window.localStorage.getItem(key);
}

function parseJwtPayload(token: string) {
  try {
    const payload = token.split(".")[1];
    if (!payload) {
      return null;
    }

    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(payload.length / 4) * 4, "=");
    return JSON.parse(window.atob(normalized)) as { exp?: number };
  } catch {
    return null;
  }
}

export function saveAuthSession(response: LoginResponse) {
  window.localStorage.setItem(tokenKey, response.access_token);
  window.localStorage.setItem(userKey, JSON.stringify(response.user));
  window.localStorage.setItem(expiresKey, response.expires_at);
  window.localStorage.setItem(accessiblePagesKey, JSON.stringify(response.user.accessible_pages ?? []));
}

export function clearAuthSession() {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(tokenKey);
  window.localStorage.removeItem(userKey);
  window.localStorage.removeItem(expiresKey);
  window.localStorage.removeItem(accessiblePagesKey);
}

export function getStoredToken() {
  return readStorage(tokenKey);
}

export function getStoredUser(): UserResponse | null {
  const raw = readStorage(userKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as UserResponse;
  } catch {
    return null;
  }
}

export function getStoredAccessiblePages() {
  const user = getStoredUser();
  if (user?.role?.toUpperCase() === "ADMIN") {
    return null;
  }

  const raw = readStorage(accessiblePagesKey);
  if (!raw) {
    return user?.accessible_pages ?? [];
  }

  try {
    return JSON.parse(raw) as string[];
  } catch {
    return [];
  }
}

export function saveAccessiblePages(pageKeys: string[]) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(accessiblePagesKey, JSON.stringify(pageKeys));
  const user = getStoredUser();
  if (user) {
    window.localStorage.setItem(userKey, JSON.stringify({ ...user, accessible_pages: pageKeys }));
  }
}

export function canAccessPage(pageKey?: string | null) {
  if (!pageKey) {
    return true;
  }

  const user = getStoredUser();
  if (user?.role?.toUpperCase() === "ADMIN") {
    return true;
  }

  return (getStoredAccessiblePages() ?? []).includes(pageKey);
}

export function isTokenExpired(token: string) {
  const payload = parseJwtPayload(token);
  const expiresAt = payload?.exp ? payload.exp * 1000 : Date.parse(readStorage(expiresKey) || "");

  if (!Number.isFinite(expiresAt)) {
    return true;
  }

  return Date.now() >= expiresAt - 30_000;
}

export function hasValidAuthSession() {
  const token = getStoredToken();
  if (!token || isTokenExpired(token)) {
    clearAuthSession();
    return false;
  }

  return true;
}

export function redirectToSignIn() {
  if (typeof window === "undefined") {
    return;
  }

  const next = `${window.location.pathname}${window.location.search}`;
  window.location.href = `/signin?next=${encodeURIComponent(next)}`;
}
