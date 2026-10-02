"use client";

import { useEffect, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet } from "@/lib/api";
import type { ProductionActivityLog } from "./types";
import { formatDateTime } from "./ui";

function formatWeight(value?: number | null) {
  return typeof value === "number" ? value.toLocaleString("en-US", { maximumFractionDigits: 3 }) : "-";
}

function formatActivity(value: string) {
  return value.replaceAll("_", " ");
}

export default function InternalSystemLogPage() {
  const toast = useToast();
  const [logs, setLogs] = useState<ProductionActivityLog[]>([]);
  const [loading, setLoading] = useState(true);

  async function loadLogs() {
    setLoading(true);

    try {
      const data = await apiGet<ProductionActivityLog[]>("/api/production/activity-logs");
      setLogs(data);
    } catch (error) {
      toast.error({ message: error instanceof Error ? error.message : "Failed to load activity logs." });
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadLogs();
  }, []);

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="font-bold text-slate-900 dark:text-white">Log Aktivitas Produksi</h1>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Menampilkan aktivitas scan, start, finish, dan perubahan status produksi terbaru.</p>
          </div>
          <button
            className="inline-flex h-10 items-center justify-center rounded-lg bg-[#0799c9] px-4 text-sm font-bold text-white shadow-sm transition-colors hover:bg-[#0688b3] focus:outline-none focus:ring-4 focus:ring-[#0799c9]/20 disabled:cursor-not-allowed disabled:opacity-60"
            disabled={loading}
            onClick={() => void loadLogs()}
            type="button"
          >
            {loading ? "Loading..." : "Refresh"}
          </button>
        </div>

        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[1200px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Time</th>
                <th className="bg-[#0799c9] px-4 py-3">Activity</th>
                <th className="bg-[#0799c9] px-4 py-3">Order No</th>
                <th className="bg-[#0799c9] px-4 py-3">Lot No</th>
                <th className="bg-[#0799c9] px-4 py-3">Project No</th>
                <th className="bg-[#0799c9] px-4 py-3 text-right">Weight</th>
                <th className="bg-[#0799c9] px-4 py-3">User</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3">Detail</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {logs.map((item) => (
                <tr className="text-sm hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{formatDateTime(item.created_at)}</td>
                  <td className="px-4 py-4">
                    <span className="w-fit rounded-full bg-cyan-50 px-2.5 py-1 text-[11px] font-bold text-cyan-700 dark:bg-cyan-500/10 dark:text-cyan-300">
                      {formatActivity(item.activity_type)}
                    </span>
                  </td>
                  <td className="px-4 py-4 font-bold text-slate-800 dark:text-white">{item.order_number || `Order #${item.production_work_order_id}`}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.lot_no || "-"}</td>
                  <td className="px-4 py-4 text-sm font-semibold text-slate-700 dark:text-slate-200">{item.project_no || "-"}</td>
                  <td className="px-4 py-4 text-right text-xs font-semibold text-slate-500 dark:text-slate-300">{formatWeight(item.weight)}</td>
                  <td className="px-4 py-4">
                    <p className="text-xs font-bold text-slate-700 dark:text-slate-200">{item.pic_name || item.username || "System"}</p>
                    <p className="mt-1 text-xs text-slate-400 dark:text-slate-400">{item.employee_no || item.username || "-"}</p>
                  </td>
                  <td className="px-5 py-4 text-sm text-slate-600 dark:text-slate-300">{item.remarks || "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {!logs.length ? (
            <div className="flex min-h-48 items-center justify-center px-5 py-10 text-center">
              <p className="text-sm font-semibold text-slate-400 dark:text-slate-300">{loading ? "Loading activity logs..." : "Belum ada log aktivitas produksi."}</p>
            </div>
          ) : null}
        </div>
      </section>
    </div>
  );
}
