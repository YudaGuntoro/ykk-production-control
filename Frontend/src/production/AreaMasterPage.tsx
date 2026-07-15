"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { apiGet, apiPost, apiPut } from "@/lib/api";
import type { AreaMaster } from "./types";

const inputClass =
  "h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-800 placeholder:text-slate-500 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-300";

const emptyForm = {
  area_code: "",
  area_name: "",
  description: "",
  is_active: true,
};

type AreaForm = typeof emptyForm;

function toForm(item: AreaMaster): AreaForm {
  return {
    area_code: item.area_code,
    area_name: item.area_name,
    description: item.description ?? "",
    is_active: item.is_active,
  };
}

export default function AreaMasterPage() {
  const [items, setItems] = useState<AreaMaster[]>([]);
  const [form, setForm] = useState<AreaForm>(emptyForm);
  const [editing, setEditing] = useState<AreaMaster | null>(null);
  const [editForm, setEditForm] = useState<AreaForm>(emptyForm);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      setItems(await apiGet<AreaMaster[]>("/api/production/area-master?page=1&pageSize=100"));
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to load area master." });
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function updateForm<K extends keyof AreaForm>(key: K, value: AreaForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function updateEditForm<K extends keyof AreaForm>(key: K, value: AreaForm[K]) {
    setEditForm((current) => ({ ...current, [key]: value }));
  }

  async function createArea(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!form.area_code.trim() || !form.area_name.trim()) {
      setMessage({ kind: "error", text: "Area code dan area name wajib diisi." });
      return;
    }

    setBusy(true);
    setMessage(null);
    try {
      await apiPost<AreaMaster>("/api/production/area-master", {
        area_code: form.area_code.trim(),
        area_name: form.area_name.trim(),
        description: form.description.trim() || null,
        is_active: form.is_active,
      });
      setForm(emptyForm);
      setMessage({ kind: "ok", text: "Area master berhasil ditambahkan." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to create area master." });
    } finally {
      setBusy(false);
    }
  }

  async function updateArea(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing) return;

    if (!editForm.area_code.trim() || !editForm.area_name.trim()) {
      setMessage({ kind: "error", text: "Area code dan area name wajib diisi." });
      return;
    }

    setBusy(true);
    setMessage(null);
    try {
      await apiPut<AreaMaster>(`/api/production/area-master/${editing.id}`, {
        area_code: editForm.area_code.trim(),
        area_name: editForm.area_name.trim(),
        description: editForm.description.trim() || null,
        is_active: editForm.is_active,
      });
      setEditing(null);
      setEditForm(emptyForm);
      setMessage({ kind: "ok", text: "Area master berhasil diupdate." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to update area master." });
    } finally {
      setBusy(false);
    }
  }

  function openEdit(item: AreaMaster) {
    setEditing(item);
    setEditForm(toForm(item));
  }

  return (
    <div className="space-y-6">
      {editing ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-4">
          <form className="w-full max-w-3xl overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:bg-slate-900" onSubmit={(event) => void updateArea(event)}>
            <div className="flex items-center justify-between bg-[#0799c9] px-5 py-3 text-white">
              <div>
                <h2 className="text-sm font-black text-white">Update Area Master</h2>
                <p className="mt-0.5 text-xs font-semibold text-cyan-50">{editing.area_code}</p>
              </div>
              <button
                aria-label="Close update area modal"
                className="flex h-8 w-8 items-center justify-center rounded-md text-sm font-black text-white/80 hover:bg-white/15 hover:text-white"
                disabled={busy}
                onClick={() => setEditing(null)}
                type="button"
              >
                X
              </button>
            </div>

            <div className="grid gap-4 p-5 md:grid-cols-2">
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Area Code</span>
                <input className={`${inputClass} mt-2 font-mono uppercase`} disabled={busy} onChange={(event) => updateEditForm("area_code", event.target.value)} value={editForm.area_code} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Area Name</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateEditForm("area_name", event.target.value)} value={editForm.area_name} />
              </label>
              <label className="block md:col-span-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Description</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateEditForm("description", event.target.value)} value={editForm.description} />
              </label>
              <label className="flex items-center gap-2 rounded-md bg-slate-50 px-3 py-3 text-sm font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200 md:col-span-2">
                <input className="h-4 w-4 rounded border-slate-300 text-[#0799c9]" checked={editForm.is_active} disabled={busy} onChange={(event) => updateEditForm("is_active", event.target.checked)} type="checkbox" />
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

      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Master Data</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Area Master</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">Master area untuk kebutuhan perluasan area produksi di masa depan.</p>
      </div>

      {message ? (
        <div className={`rounded-lg px-4 py-3 text-sm font-semibold ${message.kind === "ok" ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300" : "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-300"}`}>
          {message.text}
        </div>
      ) : null}

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div>
          <h2 className="font-bold text-slate-900 dark:text-white">Tambah Area</h2>
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Area code harus unik dan akan disimpan uppercase.</p>
        </div>
        <form className="mt-4 grid gap-4 lg:grid-cols-[0.8fr_1fr_1.4fr_auto]" onSubmit={(event) => void createArea(event)}>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Area Code</span>
            <input
              autoComplete="off"
              className={`${inputClass} mt-2 font-mono uppercase`}
              disabled={busy}
              onChange={(event) => updateForm("area_code", event.target.value)}
              placeholder="CUTTING_OUTER"
              value={form.area_code}
            />
          </label>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Area Name</span>
            <input
              autoComplete="off"
              className={`${inputClass} mt-2`}
              disabled={busy}
              onChange={(event) => updateForm("area_name", event.target.value)}
              placeholder="Cutting Outer"
              value={form.area_name}
            />
          </label>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Description</span>
            <input
              autoComplete="off"
              className={`${inputClass} mt-2`}
              disabled={busy}
              onChange={(event) => updateForm("description", event.target.value)}
              placeholder="Optional"
              value={form.description}
            />
          </label>
          <div className="flex items-end">
            <button className="h-11 w-full rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white transition hover:bg-[#087ea4] disabled:bg-[#0f5f78] disabled:text-cyan-50 disabled:opacity-100 lg:w-auto" disabled={busy} type="submit">
              Add Area
            </button>
          </div>
        </form>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
          <h2 className="font-bold text-slate-900 dark:text-white">Area List</h2>
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Data master area yang dapat dipakai oleh modul produksi berikutnya.</p>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[860px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Area Code</th>
                <th className="bg-[#0799c9] px-4 py-3">Area Name</th>
                <th className="bg-[#0799c9] px-4 py-3">Description</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {items.map((item) => (
                <tr className="hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4 font-mono text-xs font-bold text-slate-700 dark:text-slate-200">{item.area_code}</td>
                  <td className="px-4 py-4 text-sm font-black text-slate-900 dark:text-white">{item.area_name}</td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{item.description || "-"}</td>
                  <td className="px-4 py-4">
                    <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${item.is_active ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300" : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300"}`}>
                      {item.is_active ? "ACTIVE" : "INACTIVE"}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-right">
                    <button className="h-9 rounded-md bg-[#0799c9] px-3 text-xs font-bold text-white hover:bg-[#087ea4]" onClick={() => openEdit(item)} type="button">
                      Update
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!items.length ? <p className="px-5 py-12 text-center text-sm text-slate-400 dark:text-slate-200">No area master data.</p> : null}
        </div>
      </section>
    </div>
  );
}
