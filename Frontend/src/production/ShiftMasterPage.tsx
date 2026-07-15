"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { apiGet, apiPut } from "@/lib/api";
import type { ShiftMaster } from "./types";

const inputClass = "h-10 w-full rounded-md border border-slate-200 bg-white px-3 text-sm font-semibold text-slate-800 outline-none focus:border-[#0799c9] dark:border-slate-700 dark:bg-slate-950 dark:text-white";

function formatSchedule(value?: string | null) {
  if (!value) return "-";
  return value.slice(0, 5);
}

export default function ShiftMasterPage() {
  const [items, setItems] = useState<ShiftMaster[]>([]);
  const [editing, setEditing] = useState<ShiftMaster | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    await apiGet<ShiftMaster[]>("/api/production/shift-masters")
      .then((data) => {
        setItems(data);
        setError(null);
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load data."));
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing) return;

    const form = new FormData(event.currentTarget);
    setBusy(true);
    setMessage(null);
    setError(null);

    try {
      await apiPut<ShiftMaster>(`/api/production/shift-masters/${editing.id}`, {
        shift_name: form.get("shift_name"),
        shift_type: form.get("shift_type"),
        start_schedule: form.get("start_schedule"),
        finish_schedule: form.get("finish_schedule"),
        is_active: form.get("is_active") === "on",
      });
      setEditing(null);
      setMessage("Shift master updated successfully.");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update shift master.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Master Data</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Shift Master</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">Production shifts available for operator scanning.</p>
      </div>

      {message ? <div className="rounded-xl bg-emerald-50 p-4 text-sm font-semibold text-emerald-700">{message}</div> : null}
      {error ? <div className="rounded-xl bg-rose-50 p-4 text-sm font-semibold text-rose-700">{error}</div> : null}

      {editing ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-4">
          <form className="w-full max-w-3xl overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:bg-slate-900" onSubmit={(event) => void submit(event)}>
            <div className="flex items-center justify-between bg-[#0799c9] px-5 py-3 text-white">
              <div>
                <h2 className="text-sm font-black text-white">Update Shift Master</h2>
                <p className="mt-0.5 text-xs font-semibold text-cyan-50">{editing.shift_name}</p>
              </div>
              <button
                aria-label="Close update shift modal"
                className="flex h-8 w-8 items-center justify-center rounded-md text-sm font-black text-white/80 hover:bg-white/15 hover:text-white"
                disabled={busy}
                onClick={() => setEditing(null)}
                type="button"
              >
                X
              </button>
            </div>

            <div className="grid gap-4 p-5 md:grid-cols-2">
              <div>
                <label className="text-xs font-bold uppercase text-slate-500 dark:text-slate-300">Shift Name</label>
                <input className={`${inputClass} mt-2`} defaultValue={editing.shift_name} name="shift_name" required />
              </div>
              <div>
                <label className="text-xs font-bold uppercase text-slate-500 dark:text-slate-300">Type</label>
                <select className={`${inputClass} mt-2`} defaultValue={editing.shift_type ?? ""} name="shift_type">
                  <option value="">-</option>
                  <option value="Day">Day</option>
                  <option value="Middle">Middle</option>
                  <option value="Night">Night</option>
                </select>
              </div>
              <div>
                <label className="text-xs font-bold uppercase text-slate-500 dark:text-slate-300">Start Schedule</label>
                <input className={`${inputClass} mt-2`} defaultValue={formatSchedule(editing.start_schedule) === "-" ? "" : formatSchedule(editing.start_schedule)} name="start_schedule" type="time" />
              </div>
              <div>
                <label className="text-xs font-bold uppercase text-slate-500 dark:text-slate-300">Finish Schedule</label>
                <input className={`${inputClass} mt-2`} defaultValue={formatSchedule(editing.finish_schedule) === "-" ? "" : formatSchedule(editing.finish_schedule)} name="finish_schedule" type="time" />
              </div>
              <label className="flex items-center gap-2 rounded-md bg-slate-50 px-3 py-3 text-sm font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200 md:col-span-2">
                <input className="h-4 w-4 rounded border-slate-300 text-[#0799c9]" defaultChecked={editing.is_active} name="is_active" type="checkbox" />
                Active
              </label>
            </div>

            <div className="flex justify-end gap-3 border-t border-slate-100 px-5 py-4 dark:border-slate-800">
              <button className="h-10 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={() => setEditing(null)} type="button">
                Cancel
              </button>
              <button className="h-10 rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white hover:bg-[#087ea4] disabled:bg-[#0f5f78]" disabled={busy} type="submit">
                Update
              </button>
            </div>
          </form>
        </div>
      ) : null}

      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[760px] border-separate border-spacing-0 text-left text-sm">
            <thead className="text-xs uppercase text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Shift</th>
                <th className="bg-[#0799c9] px-4 py-3">Type</th>
                <th className="bg-[#0799c9] px-4 py-3">Start Schedule</th>
                <th className="bg-[#0799c9] px-4 py-3">Finish Schedule</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {items.map((item) => (
                <tr key={item.id}>
                  <td className="px-5 py-4 font-bold text-slate-800 dark:text-white">{item.shift_name}</td>
                  <td className="px-4 py-4 font-semibold text-slate-700 dark:text-slate-200">{item.shift_type || "-"}</td>
                  <td className="px-4 py-4 text-slate-600 dark:text-slate-300">{formatSchedule(item.start_schedule)}</td>
                  <td className="px-4 py-4 text-slate-600 dark:text-slate-300">{formatSchedule(item.finish_schedule)}</td>
                  <td className="px-4 py-4">
                    <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${item.is_active ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300" : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300"}`}>
                      {item.is_active ? "ACTIVE" : "INACTIVE"}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-right">
                    <button className="h-9 rounded-md bg-[#0799c9] px-3 text-xs font-bold text-white hover:bg-[#087ea4]" onClick={() => setEditing(item)} type="button">
                      Update
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!items.length && !error ? <p className="px-5 py-12 text-center text-sm text-slate-400 dark:text-slate-200">No shift master data.</p> : null}
        </div>
      </div>
    </div>
  );
}
