"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { navItems } from "@/layout/navItems";
import { apiGet } from "./api";
import { canAccessPage, getStoredUser, hasValidAuthSession, saveAccessiblePages } from "./auth";

function isPathMatch(itemPath: string, pathname: string) {
  if (itemPath === "/") {
    return pathname === "/";
  }

  return pathname === itemPath || pathname.startsWith(`${itemPath}/`);
}

function getPageKey(pathname: string) {
  const direct = navItems.find((nav) => nav.path && isPathMatch(nav.path, pathname));
  if (direct?.pageKey) {
    return direct.pageKey;
  }

  return navItems
    .flatMap((nav) => nav.subItems ?? [])
    .find((item) => isPathMatch(item.path, pathname))?.pageKey ?? null;
}

function getFirstAccessiblePath() {
  for (const nav of navItems) {
    if (nav.path && canAccessPage(nav.pageKey)) {
      return nav.path;
    }

    const subItem = nav.subItems?.find((item) => canAccessPage(item.pageKey));
    if (subItem) {
      return subItem.path;
    }
  }

  return "/signin";
}

export function AuthGuard({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const currentPath = pathname || "/";
  const [allowedPath, setAllowedPath] = useState<string | null>(null);

  useEffect(() => {
    const nextPath = pathname || "/";
    let alive = true;

    async function checkAccess() {
      if (!hasValidAuthSession()) {
        router.replace(`/signin?next=${encodeURIComponent(nextPath)}`);
        return;
      }

      const user = getStoredUser();
      if (user?.role?.toUpperCase() !== "ADMIN") {
        try {
          saveAccessiblePages(await apiGet<string[]>("/api/users/me/page-access"));
        } catch {
          // Keep the last saved access list when the permission refresh is unavailable.
        }
      }

      const pageKey = getPageKey(nextPath);
      if (!canAccessPage(pageKey)) {
        router.replace(getFirstAccessiblePath());
        return;
      }

      if (alive) {
        setAllowedPath(nextPath);
      }
    }

    void checkAccess();

    return () => {
      alive = false;
    };
  }, [pathname, router]);

  if (allowedPath !== currentPath) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-50 text-sm font-medium text-gray-500 dark:bg-gray-900 dark:text-gray-400">
        Checking session...
      </div>
    );
  }

  return children;
}
