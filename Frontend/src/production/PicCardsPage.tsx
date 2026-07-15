"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { apiGet, apiPost } from "@/lib/api";
import type { PicCard } from "./types";
import { formatDateTime } from "./ui";

const inputClass =
  "h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-800 placeholder:text-slate-500 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-300";

export default function PicCardsPage() {
  const [items, setItems] = useState<PicCard[]>([]);
  const [cardUid, setCardUid] = useState("");
  const [employeeNo, setEmployeeNo] = useState("");
  const [fullName, setFullName] = useState("");
  const [pendingDeactivate, setPendingDeactivate] = useState<PicCard | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      setItems(await apiGet<PicCard[]>("/api/production/pic-cards"));
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to load data." });
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function registerOperator(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!cardUid.trim() || !employeeNo.trim() || !fullName.trim()) {
      setMessage({ kind: "error", text: "Scan ID, NIK, dan Nama wajib diisi." });
      return;
    }

    setBusy(true);
    setMessage(null);
    try {
      await apiPost<PicCard>("/api/production/pic-cards", {
        card_uid: cardUid.trim(),
        employee_no: employeeNo.trim(),
        full_name: fullName.trim(),
      });
      setCardUid("");
      setEmployeeNo("");
      setFullName("");
      setMessage({ kind: "ok", text: "Operator berhasil didaftarkan." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to register operator." });
    } finally {
      setBusy(false);
    }
  }

  async function deactivateOperator() {
    if (!pendingDeactivate) {
      return;
    }

    setBusy(true);
    setMessage(null);
    try {
      await apiPost<PicCard>(`/api/production/pic-cards/${pendingDeactivate.id}/deactivate`);
      setMessage({ kind: "ok", text: "Operator berhasil dinonaktifkan." });
      setPendingDeactivate(null);
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to deactivate operator." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      {pendingDeactivate ? (
        <div className="fixed inset-0 z-[100010] flex items-center justify-center bg-slate-950/50 px-4">
          <div className="w-full max-w-md rounded-lg bg-white p-5 shadow-xl dark:bg-slate-900">
            <h2 className="text-lg font-black text-slate-900 dark:text-white">Anda yakin?</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
              Operator <span className="font-bold">{pendingDeactivate.full_name}</span> akan dinonaktifkan.
            </p>
            <p className="mt-2 text-xs text-slate-400 dark:text-slate-300">
              {pendingDeactivate.employee_no} / {pendingDeactivate.card_uid}
            </p>
            <div className="mt-5 grid grid-cols-2 gap-3">
              <button
                className="h-10 rounded-md bg-rose-600 text-xs font-bold text-white hover:bg-rose-700 disabled:bg-rose-900 disabled:text-rose-50 disabled:opacity-100"
                disabled={busy}
                onClick={() => void deactivateOperator()}
                type="button"
              >
                Ya, Nonaktifkan
              </button>
              <button
                className="h-10 rounded-md border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200"
                disabled={busy}
                onClick={() => setPendingDeactivate(null)}
                type="button"
              >
                Batal
              </button>
            </div>
          </div>
        </div>
      ) : null}

      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Master Data</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Operator List</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">Register scan ID operator and manage the operator list.</p>
      </div>

      {message ? (
        <div className={`rounded-lg px-4 py-3 text-sm font-semibold ${message.kind === "ok" ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300" : "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-300"}`}>
          {message.text}
        </div>
      ) : null}

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div>
          <h2 className="font-bold text-slate-900 dark:text-white">Register Operator</h2>
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Scan ID kartu operator, lalu isi NIK dan Nama.</p>
        </div>
        <form className="mt-4 grid gap-4 lg:grid-cols-[1fr_1fr_1.4fr_auto]" onSubmit={(event) => void registerOperator(event)}>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Scan ID</span>
            <input
              autoComplete="off"
              className={`${inputClass} mt-2 font-mono`}
              disabled={busy}
              onChange={(event) => setCardUid(event.target.value)}
              placeholder="Scan card ID"
              value={cardUid}
            />
          </label>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">NIK</span>
            <input
              autoComplete="off"
              className={`${inputClass} mt-2`}
              disabled={busy}
              onChange={(event) => setEmployeeNo(event.target.value)}
              placeholder="Masukkan NIK"
              value={employeeNo}
            />
          </label>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Nama</span>
            <input
              autoComplete="off"
              className={`${inputClass} mt-2`}
              disabled={busy}
              onChange={(event) => setFullName(event.target.value)}
              placeholder="Masukkan nama operator"
              value={fullName}
            />
          </label>
          <div className="flex items-end">
            <button className="h-11 w-full rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white transition hover:bg-[#087ea4] disabled:bg-[#0f5f78] disabled:text-cyan-50 disabled:opacity-100 lg:w-auto" disabled={busy} type="submit">
              Register
            </button>
          </div>
        </form>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
          <h2 className="font-bold text-slate-900 dark:text-white">List Operator</h2>
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Operator master data.</p>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[1040px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Scan ID</th>
                <th className="bg-[#0799c9] px-4 py-3">NIK</th>
                <th className="bg-[#0799c9] px-4 py-3">Nama</th>
                <th className="bg-[#0799c9] px-4 py-3">Department</th>
                <th className="bg-[#0799c9] px-4 py-3">Shift</th>
                <th className="bg-[#0799c9] px-4 py-3">Last Scan</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {items.map((item) => (
                <tr className="hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 font-mono text-xs font-bold text-slate-700 dark:text-slate-200">{item.card_uid}</td>
                  <td className="px-4 py-4 text-sm font-bold text-slate-900 dark:text-white">{item.employee_no}</td>
                  <td className="px-4 py-4 text-sm font-black text-slate-900 dark:text-white">{item.full_name}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.department}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.shift || "-"}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{formatDateTime(item.last_scanned_at)}</td>
                  <td className="px-5 py-4">
                    <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${item.is_active ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300" : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300"}`}>
                      {item.is_active ? "ACTIVE" : "INACTIVE"}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-right">
                    <button
                      className="h-9 rounded-md border border-rose-200 px-3 text-xs font-bold text-rose-600 hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-rose-500/30 dark:text-rose-300"
                      disabled={!item.is_active || busy}
                      onClick={() => setPendingDeactivate(item)}
                      type="button"
                    >
                      Nonaktifkan
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!items.length ? <p className="px-5 py-12 text-center text-sm text-slate-400 dark:text-slate-200">No operators registered.</p> : null}
        </div>
      </section>
    </div>
  );
}
