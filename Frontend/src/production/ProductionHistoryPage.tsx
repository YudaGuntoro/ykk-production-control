"use client";

import { useEffect, useMemo, useState } from "react";
import { apiGet } from "@/lib/api";
import type { ProductionActivityLog } from "./types";
import { formatDateTime, isDateMatchFilter, ProductionDateFilter, useDebouncedValue, type ProductionDateFilterMode, type ProductionDateRange } from "./ui";

export default function ProductionHistoryPage() {
  const [items, setItems] = useState<ProductionActivityLog[]>([]);
  const [dateFilterMode, setDateFilterMode] = useState<ProductionDateFilterMode>("date");
  const [dateFilter, setDateFilter] = useState("");
  const [dateRange, setDateRange] = useState<ProductionDateRange>({ startDate: "", endDate: "" });
  const [dateFilterResetKey, setDateFilterResetKey] = useState(0);
  const debouncedDateFilterMode = useDebouncedValue(dateFilterMode);
  const debouncedDateFilter = useDebouncedValue(dateFilter);
  const debouncedDateRange = useDebouncedValue(dateRange);
  useEffect(() => { void apiGet<ProductionActivityLog[]>("/api/production/activity-logs").then(setItems); }, []);

  const visibleItems = useMemo(
    () => items.filter((item) => isDateMatchFilter(item.created_at, debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange)),
    [items, debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange],
  );

  function clearDateFilter() {
    setDateFilter("");
    setDateRange({ startDate: "", endDate: "" });
    setDateFilterResetKey((current) => current + 1);
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
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[920px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">WO</th>
                <th className="bg-[#0799c9] px-4 py-3">Activity</th>
                <th className="bg-[#0799c9] px-4 py-3">Detail</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Time</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {visibleItems.map((item) => (
                <tr className="hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 text-sm font-bold text-slate-800 dark:text-white">{item.order_number || `Order #${item.production_work_order_id}`}</td>
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
