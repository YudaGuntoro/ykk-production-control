"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import React from "react";
import { navItems } from "@/layout/navItems";

interface BreadcrumbProps {
  pageTitle?: string;
  parentTitle?: string;
}

const isPathMatch = (itemPath: string, pathname: string) =>
  pathname === itemPath || pathname.startsWith(`${itemPath}/`);

const BreadcrumbSeparator = () => (
  <li className="flex items-center text-gray-400">
    <svg
      className="stroke-current"
      width="14"
      height="14"
      viewBox="0 0 20 20"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <path
        d="M7 5L12 10L7 15"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  </li>
);

const PageBreadcrumb: React.FC<BreadcrumbProps> = ({
  pageTitle,
  parentTitle,
}) => {
  const pathname = usePathname();
  const matchedNav = navItems.find((nav) => nav.path && isPathMatch(nav.path, pathname));
  const matchedSubItem = navItems
    .flatMap((nav) =>
      nav.subItems?.map((subItem) => ({
        ...subItem,
        parentTitle: nav.name,
      })) ?? []
    )
    .find((subItem) => isPathMatch(subItem.path, pathname));
  const currentPageTitle = pageTitle ?? matchedSubItem?.name ?? matchedNav?.name ?? "Dashboard";
  const matchedParentTitle =
    parentTitle ??
    matchedSubItem?.parentTitle;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 mb-6 mx-4">
      <h2
        className="text-xl font-semibold text-gray-800 dark:text-white/90"
        x-text="pageName"
      >
        {currentPageTitle}
      </h2>
      <nav>
        <ol className="flex items-center gap-2">
          <li className="flex items-center">
            <Link
              className="text-sm font-medium text-gray-500 hover:text-brand-500 dark:text-gray-400 transition-colors"
              href="/"
            >
              Home
            </Link>
          </li>
          <BreadcrumbSeparator />
          {matchedParentTitle && (
            <>
              <li className="text-sm font-medium text-gray-500 dark:text-gray-400">
                {matchedParentTitle}
              </li>
              <BreadcrumbSeparator />
            </>
          )}
          <li
            className="text-sm font-medium text-gray-800 dark:text-white/90"
            aria-current="page"
          >
            {currentPageTitle}
          </li>
        </ol>
      </nav>
    </div>
  );
};

export default PageBreadcrumb;
