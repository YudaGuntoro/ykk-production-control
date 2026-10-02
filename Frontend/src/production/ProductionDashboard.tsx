"use client";

import dynamic from "next/dynamic";
import type { ApexOptions } from "apexcharts";
import { useCallback, useEffect, useState } from "react";
import { apiGet } from "@/lib/api";
import type { ProductionDashboardShiftOutput, ProductionDashboardSummary } from "./types";
import { ProductionDatePicker, StatusBadge, todayParam } from "./ui";

const ReactApexChart = dynamic(() => import("react-apexcharts"), { ssr: false });

function MetricCard({ accent, label, value, note }: { accent: string; label: string; value: string | number; note: string }) {
  return (
    <div className="relative min-h-[124px] overflow-hidden rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-5">
      <span className={`absolute inset-x-0 top-0 h-1 ${accent}`} />
      <p className="text-xs font-bold text-slate-500 dark:text-slate-400 sm:text-sm">{label}</p>
      <p className="mt-3 text-2xl font-bold tracking-tight text-slate-900 dark:text-white sm:text-3xl">{value}</p>
      <p className="mt-2 text-[11px] font-semibold text-slate-400 sm:text-xs">{note}</p>
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
  const chartHeight = 300;

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
        fontSize: "11px",
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
      itemMargin: { horizontal: 8, vertical: 4 },
    },
    plotOptions: {
      bar: {
        borderRadius: 5,
        borderRadiusApplication: "end",
        columnWidth: "42%",
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
      labels: { rotate: 0, trim: true, style: { colors: chartMutedColor, fontSize: "11px", fontWeight: 700 } },
    },
    yaxis: {
      forceNiceScale: true,
      labels: {
        formatter: (value) => `${Math.round(Number(value))}`,
        style: { colors: chartMutedColor, fontSize: "11px", fontWeight: 700 },
      },
      max: maxValue,
      min: 0,
      tickAmount: 4,
    },
  };

  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-3 border-b border-slate-100 px-4 py-4 dark:border-slate-800 sm:flex-row sm:items-start sm:justify-between sm:px-5">
        <div className="min-w-0">
          <h2 className="font-bold text-slate-900 dark:text-white">Finished Orders by Shift</h2>
          <p className="mt-1 text-xs leading-5 text-slate-400 dark:text-slate-300">Last 5 work days up to the selected Production Date.</p>
        </div>
        <span className="w-fit rounded-md bg-slate-100 px-3 py-1 text-[11px] font-bold text-slate-500 dark:bg-slate-800 dark:text-slate-300 sm:text-xs">
          Shift 1 / Shift 2 / Shift 3
        </span>
      </div>

      <div className="min-h-[330px] overflow-hidden px-2 py-4 sm:px-4 [&_.apexcharts-canvas]:!mx-auto [&_.apexcharts-gridline]:!stroke-gray-400 [&_.apexcharts-gridline]:!opacity-100 dark:[&_.apexcharts-gridline]:!stroke-slate-500">
        {loading ? (
          <div className="flex h-[300px] items-center justify-center text-sm font-semibold text-slate-400">Loading chart...</div>
        ) : (
          <>
            <ReactApexChart options={options} series={series} type="bar" height={chartHeight} />
            {!hasData ? <p className="-mt-2 text-center text-xs font-semibold text-slate-400">No finished order data in this timeline.</p> : null}
          </>
        )}
      </div>
    </section>
  );
}

function formatWeight(value?: number | null) {
  return typeof value === "number" ? value.toLocaleString("en-US", { maximumFractionDigits: 3 }) : "-";
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
    const intervalId = window.setInterval(() => void load(), 60_000);
    return () => window.clearInterval(intervalId);
  }, [load]);

  return (
    <div className="space-y-5 sm:space-y-6">
      <section className="rounded-lg border-2 border-[#0799c9] bg-white px-4 py-5 text-slate-900 shadow-sm dark:border-[#0799c9] dark:bg-slate-900 dark:text-white sm:px-6 lg:px-7">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            <h1 className="max-w-[16rem] text-xl font-semibold leading-tight text-slate-950 dark:text-white min-[420px]:max-w-none sm:text-2xl lg:text-[28px]">Production Control Monitoring System</h1>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-600 dark:text-slate-300">Monitor daily work orders, operators, cutting lists, and finished production output.</p>
          </div>
          <ProductionDatePicker className="w-full lg:w-[220px]" label="Production Date" onChange={setDate} value={date} />
        </div>
      </section>

      {error ? <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{error} <button className="font-bold underline" onClick={() => void load()}>Try again</button></div> : null}

      <div className="grid grid-cols-1 gap-4 min-[520px]:grid-cols-2 xl:grid-cols-4">
        <MetricCard accent="bg-sky-500" label="Total Work Order" note="Selected production date" value={loading ? "..." : data?.total_work_orders ?? 0} />
        <MetricCard accent="bg-blue-500" label="Running" note="Active work orders" value={loading ? "..." : data?.running_work_orders ?? 0} />
        <MetricCard accent="bg-amber-400" label="Waiting" note="Operator / start production" value={loading ? "..." : data?.waiting_work_orders ?? 0} />
        <MetricCard accent="bg-emerald-500" label="Finished" note="Finished today" value={loading ? "..." : data?.completed_work_orders ?? 0} />
      </div>

      <FinishedShiftChart items={data?.daily_shift_outputs ?? []} loading={loading} />

      <div>
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="border-b border-slate-100 px-4 py-4 dark:border-slate-800 sm:px-5">
            <div><h2 className="font-bold text-slate-900 dark:text-white">Production Line Status</h2><p className="mt-1 text-xs text-slate-400">Work order status today</p></div>
          </div>
          <div className="hidden overflow-x-auto p-5 md:block">
            <table className="w-full min-w-[720px] border-separate border-spacing-0 text-left">
              <thead className="text-[11px] uppercase tracking-wider text-white"><tr><th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Project No</th><th className="bg-[#0799c9] px-4 py-3">Order No</th><th className="bg-[#0799c9] px-4 py-3">Lot No</th><th className="bg-[#0799c9] px-4 py-3 text-right">Weight</th><th className="rounded-r-lg bg-[#0799c9] px-5 py-3">Status</th></tr></thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {(data?.work_orders ?? []).map((order) => {
                  return (
                    <tr className="text-sm" key={order.id}>
                      <td className="px-5 py-4 font-bold text-slate-800 dark:text-white">{order.project_no || "-"}</td>
                      <td className="px-4 py-4 font-semibold text-slate-700 dark:text-slate-200">{order.order_no || "-"}</td>
                      <td className="px-4 py-4 font-semibold text-slate-700 dark:text-slate-200">{order.lot_no || "-"}</td>
                      <td className="px-4 py-4 text-right text-xs font-semibold text-slate-500 dark:text-slate-300">{formatWeight(order.weight)}</td>
                      <td className="px-5 py-4"><StatusBadge status={order.status} /></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            {!loading && !(data?.work_orders.length) ? <p className="px-5 py-12 text-center text-sm text-slate-400">No latest work orders found.</p> : null}
          </div>
          <div className="space-y-3 p-4 md:hidden">
            {(data?.work_orders ?? []).map((order) => (
              <article className="rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-slate-800 dark:bg-slate-950" key={order.id}>
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Project No</p>
                    <p className="mt-1 break-words text-sm font-black text-slate-900 dark:text-white">{order.project_no || "-"}</p>
                  </div>
                  <StatusBadge status={order.status} />
                </div>
                <dl className="mt-4 grid grid-cols-1 gap-3 text-sm min-[420px]:grid-cols-2">
                  <div>
                    <dt className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Order No</dt>
                    <dd className="mt-1 break-words font-semibold text-slate-700 dark:text-slate-200">{order.order_no || "-"}</dd>
                  </div>
                  <div>
                    <dt className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Lot No</dt>
                    <dd className="mt-1 break-words font-semibold text-slate-700 dark:text-slate-200">{order.lot_no || "-"}</dd>
                  </div>
                  <div>
                    <dt className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Weight</dt>
                    <dd className="mt-1 font-semibold text-slate-700 dark:text-slate-200">{formatWeight(order.weight)}</dd>
                  </div>
                </dl>
              </article>
            ))}
            {!loading && !(data?.work_orders.length) ? <p className="px-5 py-12 text-center text-sm text-slate-400">No latest work orders found.</p> : null}
          </div>
        </section>
      </div>
    </div>
  );
}
