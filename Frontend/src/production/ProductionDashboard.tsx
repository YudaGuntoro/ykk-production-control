"use client";

import dynamic from "next/dynamic";
import type { ApexOptions } from "apexcharts";
import { useCallback, useEffect, useMemo, useState } from "react";
import { apiGet } from "@/lib/api";
import type { ProductionDashboardShiftOutput, ProductionDashboardSummary } from "./types";
import { formatDateTime, ProductionDatePicker, StatusBadge, todayParam } from "./ui";

const ReactApexChart = dynamic(() => import("react-apexcharts"), { ssr: false });

function MetricCard({ accent, label, value, note }: { accent: string; label: string; value: string | number; note: string }) {
  return (
    <div className="relative overflow-hidden rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <span className={`absolute inset-x-0 top-0 h-1 ${accent}`} />
      <p className="text-sm font-semibold text-slate-500 dark:text-slate-400">{label}</p>
      <p className="mt-3 text-3xl font-bold tracking-tight text-slate-900 dark:text-white">{value}</p>
      <p className="mt-2 text-xs text-slate-400">{note}</p>
    </div>
  );
}

function FinishedShiftChart({ items, loading }: { items: ProductionDashboardShiftOutput[]; loading: boolean }) {
  const [isDarkMode, setIsDarkMode] = useState(false);
  const categories = items.map((item) => item.date_label);
  const hasData = items.some((item) => item.total_count > 0);
  const series = [
    { name: "Shift 1", data: items.map((item) => item.shift_1_count) },
    { name: "Shift 2", data: items.map((item) => item.shift_2_count) },
    { name: "Shift 3", data: items.map((item) => item.shift_3_count) },
  ];
  const maxValue = Math.max(4, ...items.map((item) => item.total_count));
  const chartTextColor = isDarkMode ? "#e2e8f0" : "#334155";
  const chartMutedColor = isDarkMode ? "#94a3b8" : "#64748b";
  const chartGridColor = isDarkMode ? "#64748b" : "#9ca3af";

  useEffect(() => {
    const root = document.documentElement;
    const syncTheme = () => setIsDarkMode(root.classList.contains("dark"));
    const observer = new MutationObserver(syncTheme);

    syncTheme();
    observer.observe(root, { attributes: true, attributeFilter: ["class"] });

    return () => observer.disconnect();
  }, []);

  const options: ApexOptions = {
    colors: ["#14b8a6", "#60a5fa", "#a855f7"],
    chart: {
      background: "transparent",
      foreColor: chartMutedColor,
      fontFamily: "Outfit, sans-serif",
      toolbar: { show: false },
      type: "bar",
    },
    dataLabels: {
      enabled: true,
      formatter: (value) => `${Math.round(Number(value))}`,
      offsetY: -20,
      style: {
        colors: [chartTextColor],
        fontSize: "12px",
        fontWeight: 800,
      },
    },
    grid: {
      borderColor: chartGridColor,
      strokeDashArray: 5,
      xaxis: { lines: { show: false } },
      yaxis: { lines: { show: true } },
    },
    legend: {
      fontFamily: "Outfit",
      fontWeight: 700,
      horizontalAlign: "left",
      labels: { colors: chartMutedColor },
      markers: { size: 7 },
      position: "top",
    },
    plotOptions: {
      bar: {
        borderRadius: 5,
        borderRadiusApplication: "end",
        columnWidth: "46%",
        dataLabels: { position: "top" },
      },
    },
    stroke: {
      colors: ["transparent"],
      show: true,
      width: 3,
    },
    tooltip: {
      theme: "dark",
      y: { formatter: (value) => `${Math.round(Number(value))} order` },
    },
    xaxis: {
      axisBorder: { show: false },
      axisTicks: { show: false },
      categories,
      labels: { style: { colors: chartMutedColor, fontWeight: 700 } },
    },
    yaxis: {
      forceNiceScale: true,
      labels: {
        formatter: (value) => `${Math.round(Number(value))}`,
        style: { colors: chartMutedColor, fontWeight: 700 },
      },
      max: maxValue,
      min: 0,
      tickAmount: 4,
    },
  };

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="font-bold text-slate-900 dark:text-white">Finished Orders by Shift</h2>
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Last 5 work days up to the selected Production Date.</p>
        </div>
        <span className="rounded-md bg-slate-100 px-3 py-1 text-xs font-bold text-slate-500 dark:bg-slate-800 dark:text-slate-300">
          Shift 1 / Shift 2 / Shift 3
        </span>
      </div>

      <div className="mt-5 min-h-[292px] [&_.apexcharts-gridline]:!stroke-gray-400 [&_.apexcharts-gridline]:!opacity-100 dark:[&_.apexcharts-gridline]:!stroke-slate-500">
        {loading ? (
          <div className="flex h-[260px] items-center justify-center text-sm font-semibold text-slate-400">Loading chart...</div>
        ) : (
          <>
            <ReactApexChart options={options} series={series} type="bar" height={260} />
            {!hasData ? <p className="-mt-2 text-center text-xs font-semibold text-slate-400">No finished order data in this timeline.</p> : null}
          </>
        )}
      </div>
    </section>
  );
}

export default function ProductionDashboard() {
  const [date, setDate] = useState(todayParam());
  const [data, setData] = useState<ProductionDashboardSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await apiGet<ProductionDashboardSummary>(`/api/production/dashboard?date=${date}`));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load the production dashboard.");
    } finally {
      setLoading(false);
    }
  }, [date]);

  useEffect(() => {
    void load();
  }, [load]);

  const lineCount = useMemo(() => new Set((data?.work_orders ?? []).map((item) => item.line_code)).size, [data]);

  return (
    <div className="space-y-6">
      <section className="rounded-lg border-2 border-[#0799c9] bg-white px-6 py-5 text-slate-900 shadow-sm dark:border-[#0799c9] dark:bg-slate-900 dark:text-white sm:px-7">
        <div className="flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-950 dark:text-white sm:text-[28px]">Production Control Monitoring System</h1>
            <p className="mt-2 max-w-2xl text-sm text-slate-600 dark:text-slate-300">Monitor daily work orders, operators, cutting lists, and finished production output.</p>
          </div>
          <ProductionDatePicker className="w-full sm:w-[220px]" label="Production Date" onChange={setDate} value={date} />
        </div>
      </section>

      {error ? <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error} <button className="font-bold underline" onClick={() => void load()}>Try again</button></div> : null}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard accent="bg-sky-500" label="Total Work Order" note={`${lineCount} production line`} value={loading ? "..." : data?.total_work_orders ?? 0} />
        <MetricCard accent="bg-blue-500" label="Running" note="Active work orders" value={loading ? "..." : data?.running_work_orders ?? 0} />
        <MetricCard accent="bg-amber-400" label="Waiting" note="Operator / start production" value={loading ? "..." : data?.waiting_work_orders ?? 0} />
        <MetricCard accent="bg-emerald-500" label="Finished" note="Finished today" value={loading ? "..." : data?.completed_work_orders ?? 0} />
      </div>

      <FinishedShiftChart items={data?.daily_shift_outputs ?? []} loading={loading} />

      <div>
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
            <div><h2 className="font-bold text-slate-900 dark:text-white">Production Line Status</h2><p className="mt-1 text-xs text-slate-400">Work order status today</p></div>
          </div>
          <div className="overflow-x-auto p-5">
            <table className="w-full min-w-[960px] border-separate border-spacing-0 text-left">
              <thead className="text-[11px] uppercase tracking-wider text-white"><tr><th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Work Order</th><th className="bg-[#0799c9] px-4 py-3">Line / Product</th><th className="bg-[#0799c9] px-4 py-3">Operator</th><th className="bg-[#0799c9] px-4 py-3">Operators</th><th className="bg-[#0799c9] px-4 py-3">Actual</th><th className="bg-[#0799c9] px-4 py-3">Started</th><th className="bg-[#0799c9] px-4 py-3">Finished</th><th className="rounded-r-lg bg-[#0799c9] px-5 py-3">Status</th></tr></thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {(data?.work_orders ?? []).map((order) => {
                  return (
                    <tr className="text-sm" key={order.id}>
                      <td className="px-5 py-4"><p className="font-bold text-slate-800 dark:text-white">{order.order_number}</p><p className="mt-1 text-xs text-slate-400">{order.cutting_list_no}</p></td>
                      <td className="px-4 py-4"><p className="font-semibold text-slate-700 dark:text-slate-200">{order.line_code}</p><p className="mt-1 max-w-[220px] truncate text-xs text-slate-400">{order.product_name}</p></td>
                      <td className="px-4 py-4">
                        {order.operators?.length ? (
                          <div className="space-y-1">
                            {order.operators.map((operator) => (
                              <p className="text-xs font-semibold text-slate-700 dark:text-slate-200" key={operator.id}>
                                {operator.full_name} <span className="text-slate-400">/ {operator.shift}</span>
                              </p>
                            ))}
                          </div>
                        ) : (
                          <>
                            <p className="font-semibold text-slate-700 dark:text-slate-200">Not scanned</p>
                            <p className="mt-1 text-xs text-slate-400">-</p>
                          </>
                        )}
                      </td>
                      <td className="px-4 py-4">
                        <span className="rounded-md bg-slate-100 px-2.5 py-1 text-xs font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200">{order.operators?.length ?? 0} operator</span>
                      </td>
                      <td className="px-4 py-4">
                        {order.completed_at ? (
                          <div><p className="text-sm font-black text-slate-900 dark:text-white">{order.actual_qty.toLocaleString("en-US")}</p><p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Reject {order.reject_qty.toLocaleString("en-US")}</p></div>
                        ) : (
                          <span className="text-sm font-bold text-slate-400">-</span>
                        )}
                      </td>
                      <td className="px-4 py-4 text-xs font-semibold text-slate-500">{formatDateTime(order.started_at)}</td>
                      <td className="px-4 py-4 text-xs font-semibold text-slate-500">{formatDateTime(order.completed_at)}</td>
                      <td className="px-5 py-4"><StatusBadge status={order.status} /></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            {!loading && !(data?.work_orders.length) ? <p className="px-5 py-12 text-center text-sm text-slate-400">No work orders for this date.</p> : null}
          </div>
        </section>
      </div>
    </div>
  );
}
