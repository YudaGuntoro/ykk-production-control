"use client";

import { useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, getApiBaseUrl } from "@/lib/api";
import { getStoredToken } from "@/lib/auth";
import { ExcelIcon } from "@/icons";
import type { ProductionActivityLog } from "./types";
import { formatDateTime, isDateMatchFilter, ProductionDateFilter, useDebouncedValue, type ProductionDateFilterMode, type ProductionDateRange } from "./ui";

function getExportFileName(response: Response) {
  const contentDisposition = response.headers.get("content-disposition");
  const match = contentDisposition?.match(/filename\*=UTF-8''([^;]+)|filename="?([^"]+)"?/i);
  const fileName = match?.[1] || match?.[2];

  if (fileName) {
    try {
      return decodeURIComponent(fileName);
    } catch {
      return fileName;
    }
  }

  return `Production-Activity-${new Date().toISOString().slice(0, 10)}.xlsx`;
}

function buildActivityLogQuery(mode: ProductionDateFilterMode, date: string, range: ProductionDateRange) {
  const params = new URLSearchParams();

  if (mode === "date" && date) {
    params.set("date", date);
  }

  if (mode === "range") {
    if (range.startDate) params.set("startDate", range.startDate);
    if (range.endDate) params.set("endDate", range.endDate);
  }

  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

function formatWeight(value?: number | null) {
  return typeof value === "number" ? value.toLocaleString("en-US", { maximumFractionDigits: 3 }) : "-";
}

export default function ProductionHistoryPage() {
  const toast = useToast();
  const [items, setItems] = useState<ProductionActivityLog[]>([]);
  const [exporting, setExporting] = useState(false);
  const [dateFilterMode, setDateFilterMode] = useState<ProductionDateFilterMode>("date");
  const [dateFilter, setDateFilter] = useState("");
  const [dateRange, setDateRange] = useState<ProductionDateRange>({ startDate: "", endDate: "" });
  const [dateFilterResetKey, setDateFilterResetKey] = useState(0);
  const debouncedDateFilterMode = useDebouncedValue(dateFilterMode);
  const debouncedDateFilter = useDebouncedValue(dateFilter);
  const debouncedDateRange = useDebouncedValue(dateRange);
  useEffect(() => {
    let isActive = true;

    void apiGet<ProductionActivityLog[]>(`/api/production/activity-logs${buildActivityLogQuery(debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange)}`)
      .then((data) => {
        if (isActive) {
          setItems(data);
        }
      })
      .catch((error) => {
        if (isActive) {
          toast.error({ message: error instanceof Error ? error.message : "Failed to load production activity." });
        }
      });

    return () => {
      isActive = false;
    };
  }, [debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange, toast]);

  const visibleItems = useMemo(
    () => items.filter((item) => isDateMatchFilter(item.created_at, debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange)),
    [items, debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange],
  );

  function clearDateFilter() {
    setDateFilter("");
    setDateRange({ startDate: "", endDate: "" });
    setDateFilterResetKey((current) => current + 1);
  }

  async function handleExport() {
    setExporting(true);

    try {
      const token = getStoredToken();
      const headers = new Headers();
      if (token) {
        headers.set("Authorization", `Bearer ${token}`);
      }

      const response = await fetch(`${getApiBaseUrl()}/api/production/activity-logs/export${buildActivityLogQuery(dateFilterMode, dateFilter, dateRange)}`, {
        headers,
      });

      if (!response.ok) {
        throw new Error((await response.text()) || `Export failed with status ${response.status}`);
      }

      const blob = await response.blob();
      const downloadUrl = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = downloadUrl;
      link.download = getExportFileName(response);
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(downloadUrl);
      toast.success({ message: "Production activity export downloaded." });
    } catch (error) {
      toast.error({ message: error instanceof Error ? error.message : "Failed to export production activity." });
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="space-y-6">
      <div><p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Traceability</p><h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Production Activity</h1></div>
      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-4 border-b border-slate-100 px-5 py-4 dark:border-slate-800 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">Activity History</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Filter activity by date or date range.</p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <ProductionDateFilter
              date={dateFilter}
              mode={dateFilterMode}
              onClear={clearDateFilter}
              onDateChange={setDateFilter}
              onModeChange={setDateFilterMode}
              onRangeChange={setDateRange}
              range={dateRange}
              resetKey={dateFilterResetKey}
            />
            <button
              className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[#107C41] px-4 text-sm font-bold text-white shadow-sm transition-colors hover:bg-[#0b6533] focus:outline-none focus:ring-4 focus:ring-[#107C41]/20 disabled:cursor-not-allowed disabled:opacity-60"
              disabled={exporting}
              onClick={handleExport}
              type="button"
            >
              <span className="inline-flex size-5 shrink-0 items-center justify-center">
                <ExcelIcon className="size-5" />
              </span>
              <span>{exporting ? "Exporting..." : "Export Excel"}</span>
            </button>
          </div>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[1200px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Order No</th>
                <th className="bg-[#0799c9] px-4 py-3">Lot No</th>
                <th className="bg-[#0799c9] px-4 py-3">Project No</th>
                <th className="bg-[#0799c9] px-4 py-3 text-right">Weight</th>
                <th className="bg-[#0799c9] px-4 py-3">Activity</th>
                <th className="bg-[#0799c9] px-4 py-3">Detail</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Time</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {visibleItems.map((item) => (
                <tr className="hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 text-sm font-bold text-slate-800 dark:text-white">{item.order_number || `Order #${item.production_work_order_id}`}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.lot_no || "-"}</td>
                  <td className="px-4 py-4 text-sm font-semibold text-slate-700 dark:text-slate-200">{item.project_no || "-"}</td>
                  <td className="px-4 py-4 text-right text-xs font-semibold text-slate-500 dark:text-slate-300">{formatWeight(item.weight)}</td>
                  <td className="px-4 py-4">
                    <span className="w-fit rounded-full bg-cyan-50 px-2.5 py-1 text-[11px] font-bold text-cyan-700 dark:bg-cyan-500/10 dark:text-cyan-300">{item.activity_type.replaceAll("_", " ")}</span>
                  </td>
                  <td className="px-4 py-4">
                    <p className="text-sm text-slate-600 dark:text-slate-300">{item.remarks || "-"}</p>
                    <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">{item.pic_name || "System"}</p>
                  </td>
                  <td className="px-5 py-4 text-right text-xs font-semibold text-slate-400 dark:text-slate-300">{formatDateTime(item.created_at)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {!visibleItems.length ? (
            <div className="flex min-h-[360px] items-center justify-center px-5 py-12 lg:min-h-[calc(100vh-420px)]">
              <p className="text-center text-sm text-slate-400 dark:text-slate-200">No production activity found for the selected date.</p>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
