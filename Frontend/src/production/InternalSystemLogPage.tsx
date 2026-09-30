"use client";

import { useEffect, useState } from "react";

type InternalLogItem = {
  id: string;
  url: string;
  method: "GET";
  statusCode: number | null;
  statusText: string;
  ok: boolean;
  durationMs: number;
  contentType: string;
  detail: string;
  createdAt: string;
};

const logStorageKey = "internal-system-logs";

function readStoredLogs() {
  if (typeof window === "undefined") return [];

  try {
    return JSON.parse(window.localStorage.getItem(logStorageKey) || "[]") as InternalLogItem[];
  } catch {
    return [];
  }
}

function statusClassName(item: InternalLogItem) {
  if (item.ok) {
    return "bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-200 dark:ring-emerald-500/30";
  }

  if (item.statusCode === null) {
    return "bg-red-50 text-red-700 ring-red-200 dark:bg-red-500/10 dark:text-red-200 dark:ring-red-500/30";
  }

  return "bg-amber-50 text-amber-700 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-200 dark:ring-amber-500/30";
}

export default function InternalSystemLogPage() {
  const [logs, setLogs] = useState<InternalLogItem[]>([]);

  useEffect(() => {
    setLogs(readStoredLogs());
  }, []);

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
          <h1 className="font-bold text-slate-900 dark:text-white">Log</h1>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[1100px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Time</th>
                <th className="bg-[#0799c9] px-4 py-3">Method</th>
                <th className="bg-[#0799c9] px-4 py-3">Status Code</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="bg-[#0799c9] px-4 py-3">URL</th>
                <th className="bg-[#0799c9] px-4 py-3">Duration</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3">Detail</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {logs.map((item) => (
                <tr className="text-sm hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{new Date(item.createdAt).toLocaleString()}</td>
                  <td className="px-4 py-4">
                    <span className="rounded-full bg-slate-900 px-2.5 py-1 text-[10px] font-black text-white dark:bg-white dark:text-slate-900">{item.method}</span>
                  </td>
                  <td className="px-4 py-4">
                    <span className={`rounded-full px-2.5 py-1 text-[10px] font-black uppercase ring-1 ${statusClassName(item)}`}>
                      {item.statusCode === null ? "No Status" : item.statusCode}
                    </span>
                  </td>
                  <td className="px-4 py-4">
                    <div className="space-y-1">
                      <p className="text-xs font-bold text-slate-700 dark:text-slate-200">{item.ok ? "SUCCESS" : "FAILED"}</p>
                      <p className="text-xs font-semibold text-slate-400 dark:text-slate-400">{item.statusText}</p>
                    </div>
                  </td>
                  <td className="max-w-[360px] break-all px-4 py-4 text-xs font-semibold text-slate-600 dark:text-slate-300">{item.url}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.durationMs} ms</td>
                  <td className="px-5 py-4">
                    <pre className="max-h-28 max-w-[360px] overflow-auto whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 font-mono text-[11px] text-slate-700 dark:border-slate-800 dark:bg-slate-950 dark:text-slate-200">
                      {item.detail || "-"}
                    </pre>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {!logs.length ? (
            <div className="flex min-h-48 items-center justify-center px-5 py-10 text-center">
              <p className="text-sm font-semibold text-slate-400 dark:text-slate-300">Belum ada log endpoint internal.</p>
            </div>
          ) : null}
        </div>
      </section>
    </div>
  );
}
