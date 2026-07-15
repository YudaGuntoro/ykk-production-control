"use client";

import { useEffect, useMemo, useState } from "react";
import { apiGet } from "@/lib/api";
import type { CuttingList, ProductionOperator } from "./types";
import { formatDateTime, isDateMatchFilter, ProductionDateFilter, useDebouncedValue, type ProductionDateFilterMode, type ProductionDateRange } from "./ui";

type CuttingListStatus = CuttingList["status"];

const statusOptions: Array<CuttingListStatus | "ALL"> = ["ALL", "WAITING", "IN_PROGRESS", "FINISH"];

const cuttingListStatusStyles: Record<CuttingListStatus, string> = {
  WAITING: "bg-amber-50 text-amber-700 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-500/20",
  IN_PROGRESS: "bg-cyan-50 text-blue-700 ring-blue-200 dark:bg-cyan-500/10 dark:text-cyan-300 dark:ring-cyan-500/30",
  FINISH: "bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/30",
};

function OperatorList({ emptyText, operators }: { emptyText: string; operators: ProductionOperator[] }) {
  return (
    <div className="mt-3 grid gap-2">
      {operators.length ? operators.map((operator) => (
        <div className="rounded-md bg-slate-50 px-3 py-2 dark:bg-slate-800" key={operator.id}>
          <div className="flex items-center justify-between gap-3">
            <p className="truncate font-black text-slate-900 dark:text-white">{operator.full_name}</p>
            <span className="shrink-0 rounded-md bg-white px-2 py-1 text-[11px] font-bold text-[#087ea4] dark:bg-slate-900 dark:text-cyan-300">{operator.work_shift_name || operator.shift || "-"}</span>
          </div>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-300">{operator.employee_no} / {operator.department}</p>
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Scan {formatDateTime(operator.scanned_at)}</p>
        </div>
      )) : (
        <p className="rounded-md bg-slate-50 px-3 py-4 text-sm font-semibold text-slate-400 dark:bg-slate-800 dark:text-slate-200">{emptyText}</p>
      )}
    </div>
  );
}

export default function CuttingListsPage() {
  const [items, setItems] = useState<CuttingList[]>([]);
  const [statusFilter, setStatusFilter] = useState<CuttingListStatus | "ALL">("ALL");
  const [dateFilterMode, setDateFilterMode] = useState<ProductionDateFilterMode>("date");
  const [dateFilter, setDateFilter] = useState("");
  const [dateRange, setDateRange] = useState<ProductionDateRange>({ startDate: "", endDate: "" });
  const [dateFilterResetKey, setDateFilterResetKey] = useState(0);
  const [selectedItem, setSelectedItem] = useState<CuttingList | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const debouncedStatusFilter = useDebouncedValue(statusFilter);
  const debouncedDateFilterMode = useDebouncedValue(dateFilterMode);
  const debouncedDateFilter = useDebouncedValue(dateFilter);
  const debouncedDateRange = useDebouncedValue(dateRange);

  useEffect(() => {
    void apiGet<CuttingList[]>("/api/production/cutting-lists")
      .then(setItems)
      .catch((err) => setMessage(err instanceof Error ? err.message : "Failed to load data."));
  }, []);

  const visibleItems = useMemo(
    () => items
      .filter((item) => debouncedStatusFilter === "ALL" || item.status === debouncedStatusFilter)
      .filter((item) => isDateMatchFilter(item.created_at, debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange))
      .sort((a, b) => Date.parse(b.created_at) - Date.parse(a.created_at)),
    [items, debouncedStatusFilter, debouncedDateFilterMode, debouncedDateFilter, debouncedDateRange],
  );
  const selectedOperators = selectedItem?.operators ?? [];
  const selectedStartOperators = selectedItem?.start_operators ?? [];
  const selectedFinishOperators = selectedItem?.finish_operators ?? [];

  function clearDateFilter() {
    setDateFilter("");
    setDateRange({ startDate: "", endDate: "" });
    setDateFilterResetKey((current) => current + 1);
  }

  return (
    <div className="space-y-6">
      {selectedItem ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-6">
          <section className="flex max-h-[calc(100vh-3rem)] w-full max-w-3xl flex-col overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:border-[#0799c9] dark:bg-slate-900">
            <div className="flex shrink-0 items-center justify-between bg-[#0799c9] px-4 py-2.5 text-white">
              <div>
                <h2 className="text-xs font-bold text-white">Cutting List Detail</h2>
                <p className="mt-0.5 text-[10px] font-semibold text-cyan-50">{selectedItem.cutting_list_no}</p>
              </div>
              <button className="flex h-6 w-6 items-center justify-center rounded-md text-[11px] font-black text-white/80 hover:bg-white/15 hover:text-white" onClick={() => setSelectedItem(null)} type="button" aria-label="Close detail">
                X
              </button>
            </div>
            <div className="grid gap-4 overflow-y-auto p-5 text-sm sm:grid-cols-2">
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Cutting List</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedItem.cutting_list_no}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Scanned Date</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatDateTime(selectedItem.created_at)}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Product</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedItem.product_name}</p>
                <p className="mt-0.5 text-xs text-slate-400 dark:text-slate-300">{selectedItem.product_code}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Line</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedItem.line_code}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Status</p>
                <span className={`mt-2 inline-flex rounded-full px-2.5 py-1 text-xs font-bold ring-1 ring-inset ${cuttingListStatusStyles[selectedItem.status]}`}>{selectedItem.status.replaceAll("_", " ")}</span>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Order Number</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedItem.order_number || "-"}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Start WO</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatDateTime(selectedItem.started_at)}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Finish WO</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatDateTime(selectedItem.completed_at)}</p>
              </div>
              <div className="sm:col-span-2">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Operator yang Mengerjakan</p>
                  <span className="rounded-md bg-slate-100 px-2 py-1 text-[11px] font-bold text-slate-600 dark:bg-slate-800 dark:text-slate-200">{selectedOperators.length} operator</span>
                </div>
                <OperatorList emptyText="Belum ada operator untuk order ini." operators={selectedOperators} />
              </div>
              <div className="sm:col-span-2">
                <div className="grid gap-3 lg:grid-cols-2">
                  <div className="rounded-md border border-slate-200 p-3 dark:border-slate-700">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Operator Saat Start WO</p>
                      <span className="rounded-md bg-slate-100 px-2 py-1 text-[11px] font-bold text-slate-600 dark:bg-slate-800 dark:text-slate-200">{selectedStartOperators.length} operator</span>
                    </div>
                    <OperatorList emptyText="Belum ada operator saat Start WO." operators={selectedStartOperators} />
                  </div>
                  <div className="rounded-md border border-slate-200 p-3 dark:border-slate-700">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Operator Saat Finish WO</p>
                      <span className="rounded-md bg-slate-100 px-2 py-1 text-[11px] font-bold text-slate-600 dark:bg-slate-800 dark:text-slate-200">{selectedFinishOperators.length} operator</span>
                    </div>
                    <OperatorList emptyText={selectedItem.completed_at ? "Belum ada operator saat Finish WO." : "WO belum Finish."} operators={selectedFinishOperators} />
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>
      ) : null}

      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Traceability</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Cutting List History</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">Read-only cutting list data. Integration source is coming soon.</p>
      </div>
      {message ? <div className="rounded-xl border border-cyan-200 bg-cyan-50 px-4 py-3 text-sm text-cyan-800">{message}</div> : null}
      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">Cutting List History</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Sorted by latest scanned time, newest first.</p>
          </div>
          <div className="flex flex-col gap-3 xl:flex-row xl:items-end">
            <label className="block min-w-[150px]">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Status</span>
              <select
                className="mt-2 h-11 w-full rounded-lg border border-gray-300 bg-white px-3 text-sm font-semibold text-gray-800 shadow-theme-xs outline-none focus:border-brand-400 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
                onChange={(event) => setStatusFilter(event.target.value as CuttingListStatus | "ALL")}
                value={statusFilter}
              >
                {statusOptions.map((status) => (
                  <option key={status} value={status}>{status === "ALL" ? "ALL STATUS" : status.replaceAll("_", " ")}</option>
                ))}
              </select>
            </label>
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
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[980px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Cutting List</th>
                <th className="bg-[#0799c9] px-4 py-3">Product</th>
                <th className="bg-[#0799c9] px-4 py-3">Line</th>
                <th className="bg-[#0799c9] px-4 py-3">Scanned Date</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {visibleItems.map((item) => (
                <tr className="text-sm hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 font-bold text-slate-900 dark:text-white">{item.cutting_list_no}</td>
                  <td className="px-4 py-4"><p className="font-semibold text-slate-700 dark:text-slate-200">{item.product_name}</p><p className="mt-1 text-xs text-slate-400 dark:text-slate-300">{item.product_code}</p></td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.line_code}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{formatDateTime(item.created_at)}</td>
                  <td className="px-4 py-4"><span className={`rounded-full px-2.5 py-1 text-xs font-bold ring-1 ring-inset ${cuttingListStatusStyles[item.status]}`}>{item.status.replaceAll("_", " ")}</span></td>
                  <td className="px-5 py-4 text-right">
                    <button className="h-9 rounded-md border border-slate-200 px-3 text-xs font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" onClick={() => setSelectedItem(item)} type="button">
                      Detail
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!visibleItems.length ? (
            <div className="flex min-h-[360px] items-center justify-center px-5 py-12 lg:min-h-[calc(100vh-420px)]">
              <p className="text-center text-sm text-slate-400 dark:text-slate-200">No cutting list history found for the selected filter.</p>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
