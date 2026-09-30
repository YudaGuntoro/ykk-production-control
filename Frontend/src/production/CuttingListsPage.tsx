"use client";

import { useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, getApiBaseUrl } from "@/lib/api";
import { getStoredToken } from "@/lib/auth";
import { ExcelIcon } from "@/icons";
import type { CuttingList, ProductionOperator } from "./types";
import { formatDateTime, isDateMatchFilter, ProductionDateFilter, useDebouncedValue, type ProductionDateFilterMode, type ProductionDateRange } from "./ui";

type CuttingListStatus = CuttingList["status"];

const statusOptions: Array<CuttingListStatus | "ALL"> = ["ALL", "WAITING", "IN_PROGRESS", "FINISH"];

const cuttingListStatusStyles: Record<CuttingListStatus, string> = {
  WAITING: "bg-amber-50 text-amber-700 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-500/20",
  IN_PROGRESS: "bg-cyan-50 text-blue-700 ring-blue-200 dark:bg-cyan-500/10 dark:text-cyan-300 dark:ring-cyan-500/30",
  FINISH: "bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/30",
};

function formatWeight(value?: number | null) {
  return typeof value === "number" ? value.toLocaleString("en-US", { maximumFractionDigits: 3 }) : "-";
}

function getExportFileName(response: Response, fallback: string) {
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

  return fallback;
}

function buildExportQuery(status: CuttingListStatus | "ALL", mode: ProductionDateFilterMode, date: string, range: ProductionDateRange) {
  const params = new URLSearchParams();

  if (status !== "ALL") {
    params.set("status", status);
  }

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
  const toast = useToast();
  const [items, setItems] = useState<CuttingList[]>([]);
  const [statusFilter, setStatusFilter] = useState<CuttingListStatus | "ALL">("ALL");
  const [dateFilterMode, setDateFilterMode] = useState<ProductionDateFilterMode>("date");
  const [dateFilter, setDateFilter] = useState("");
  const [dateRange, setDateRange] = useState<ProductionDateRange>({ startDate: "", endDate: "" });
  const [dateFilterResetKey, setDateFilterResetKey] = useState(0);
  const [selectedItem, setSelectedItem] = useState<CuttingList | null>(null);
  const [exportingList, setExportingList] = useState(false);
  const [exportingId, setExportingId] = useState<number | null>(null);
  const debouncedStatusFilter = useDebouncedValue(statusFilter);
  const debouncedDateFilterMode = useDebouncedValue(dateFilterMode);
  const debouncedDateFilter = useDebouncedValue(dateFilter);
  const debouncedDateRange = useDebouncedValue(dateRange);

  useEffect(() => {
    void apiGet<CuttingList[]>("/api/production/cutting-lists")
      .then(setItems)
      .catch((err) => toast.error({ message: err instanceof Error ? err.message : "Failed to load data." }));
  }, [toast]);

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

  async function downloadExport(path: string, fallbackFileName: string) {
    const token = getStoredToken();
    const headers = new Headers();
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    const response = await fetch(`${getApiBaseUrl()}${path}`, { headers });

    if (!response.ok) {
      throw new Error((await response.text()) || `Export failed with status ${response.status}`);
    }

    const blob = await response.blob();
    const downloadUrl = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = downloadUrl;
    link.download = getExportFileName(response, fallbackFileName);
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(downloadUrl);
  }

  async function exportList() {
    setExportingList(true);

    try {
      await downloadExport(
        `/api/production/cutting-lists/export${buildExportQuery(statusFilter, dateFilterMode, dateFilter, dateRange)}`,
        `Production-History-${new Date().toISOString().slice(0, 10)}.xlsx`,
      );
      toast.success({ message: "Production history export downloaded." });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to export production history." });
    } finally {
      setExportingList(false);
    }
  }

  async function exportDetail(item: CuttingList) {
    setExportingId(item.id);

    try {
      await downloadExport(
        `/api/production/cutting-lists/${item.id}/export`,
        `Production-History-${item.lot_no || item.order_number || item.id}.xlsx`,
      );
      toast.success({ message: "Production history detail export downloaded." });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to export production history detail." });
    } finally {
      setExportingId(null);
    }
  }

  return (
    <div className="space-y-6">
      {selectedItem ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-6">
          <section className="flex max-h-[calc(100vh-3rem)] w-full max-w-3xl flex-col overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:border-[#0799c9] dark:bg-slate-900">
            <div className="flex shrink-0 items-center justify-between bg-[#0799c9] px-4 py-2.5 text-white">
              <div>
                <h2 className="text-xs font-bold text-white">Production History Detail</h2>
                <p className="mt-0.5 text-[10px] font-semibold text-cyan-50">{selectedItem.lot_no || selectedItem.order_number || "-"}</p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  className="inline-flex h-8 items-center justify-center gap-1.5 rounded-md bg-white/15 px-3 text-[11px] font-bold text-white hover:bg-white/25 disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={exportingId === selectedItem.id}
                  onClick={() => void exportDetail(selectedItem)}
                  type="button"
                >
                  <ExcelIcon className="size-4" />
                  {exportingId === selectedItem.id ? "Exporting..." : "Export"}
                </button>
                <button className="flex h-6 w-6 items-center justify-center rounded-md text-[11px] font-black text-white/80 hover:bg-white/15 hover:text-white" onClick={() => setSelectedItem(null)} type="button" aria-label="Close detail">
                  X
                </button>
              </div>
            </div>
            <div className="grid gap-4 overflow-y-auto p-5 text-sm sm:grid-cols-2">
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Scanned Date</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatDateTime(selectedItem.created_at)}</p>
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
                <p className="text-xs text-slate-400 dark:text-slate-300">Lot No</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedItem.lot_no || "-"}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Project No</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedItem.project_no || "-"}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Weight</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatWeight(selectedItem.weight)}</p>
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
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Production History</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">Read-only cutting list data. Integration source is coming soon.</p>
      </div>
      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">Production History</h2>
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
            <button
              className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[#107C41] px-4 text-sm font-bold text-white shadow-sm transition-colors hover:bg-[#0b6533] focus:outline-none focus:ring-4 focus:ring-[#107C41]/20 disabled:cursor-not-allowed disabled:opacity-60"
              disabled={exportingList}
              onClick={() => void exportList()}
              type="button"
            >
              <span className="inline-flex size-5 shrink-0 items-center justify-center">
                <ExcelIcon className="size-5" />
              </span>
              <span>{exportingList ? "Exporting..." : "Export List"}</span>
            </button>
          </div>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[980px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Lot No</th>
                <th className="bg-[#0799c9] px-4 py-3">Project No</th>
                <th className="bg-[#0799c9] px-4 py-3 text-right">Weight</th>
                <th className="bg-[#0799c9] px-4 py-3">Line</th>
                <th className="bg-[#0799c9] px-4 py-3">Scanned Date</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {visibleItems.map((item) => (
                <tr className="text-sm hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 font-bold text-slate-900 dark:text-white">{item.lot_no || "-"}</td>
                  <td className="px-4 py-4 font-semibold text-slate-700 dark:text-slate-200">{item.project_no || "-"}</td>
                  <td className="px-4 py-4 text-right text-xs font-semibold text-slate-500 dark:text-slate-300">{formatWeight(item.weight)}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.line_code}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{formatDateTime(item.created_at)}</td>
                  <td className="px-4 py-4"><span className={`rounded-full px-2.5 py-1 text-xs font-bold ring-1 ring-inset ${cuttingListStatusStyles[item.status]}`}>{item.status.replaceAll("_", " ")}</span></td>
                  <td className="px-5 py-4 text-right">
                    <div className="flex justify-end gap-2">
                      <button
                        className="h-9 rounded-md border border-emerald-200 px-3 text-xs font-bold text-emerald-700 hover:border-emerald-500 hover:bg-emerald-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-emerald-500/40 dark:text-emerald-300 dark:hover:bg-emerald-500/10"
                        disabled={exportingId === item.id}
                        onClick={() => void exportDetail(item)}
                        type="button"
                      >
                        {exportingId === item.id ? "Exporting..." : "Export"}
                      </button>
                      <button className="h-9 rounded-md border border-slate-200 px-3 text-xs font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" onClick={() => setSelectedItem(item)} type="button">
                        Detail
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!visibleItems.length ? (
            <div className="flex min-h-[360px] items-center justify-center px-5 py-12 lg:min-h-[calc(100vh-420px)]">
              <p className="text-center text-sm text-slate-400 dark:text-slate-200">No production history found for the selected filter.</p>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
