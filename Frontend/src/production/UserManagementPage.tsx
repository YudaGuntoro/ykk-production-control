"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, apiPut } from "@/lib/api";
import type { LoginUser, LoginUserRole, LoginUserStatus } from "./types";
import { formatDateTime } from "./ui";

const inputClass =
  "h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-800 placeholder:text-slate-500 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-300";

const roleOptions: LoginUserRole[] = ["ADMIN", "SUPERVISOR", "OPERATOR", "VIEWER"];
const statusOptions: LoginUserStatus[] = ["ACTIVE", "INACTIVE"];

type UserForm = {
  username: string;
  full_name: string;
  email: string;
  phone: string;
  role: LoginUserRole;
  status: LoginUserStatus;
  password: string;
};

const emptyForm: UserForm = {
  username: "",
  full_name: "",
  email: "",
  phone: "",
  role: "VIEWER",
  status: "ACTIVE",
  password: "",
};

function toForm(user: LoginUser): UserForm {
  return {
    username: user.username,
    full_name: user.full_name,
    email: user.email ?? "",
    phone: user.phone ?? "",
    role: user.role,
    status: user.status,
    password: "",
  };
}

function statusClass(status: LoginUserStatus) {
  return status === "ACTIVE"
    ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
    : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300";
}

export default function UserManagementPage() {
  const toast = useToast();
  const [items, setItems] = useState<LoginUser[]>([]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<LoginUserStatus | "ALL">("ALL");
  const [editing, setEditing] = useState<LoginUser | null>(null);
  const [form, setForm] = useState<UserForm>(emptyForm);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "200" });
      if (search.trim()) params.set("search", search.trim());
      if (statusFilter !== "ALL") params.set("status", statusFilter);
      setItems(await apiGet<LoginUser[]>(`/api/users?${params.toString()}`));
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal load user." });
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const roleSummary = useMemo(
    () => roleOptions.map((role) => ({ role, count: items.filter((item) => item.role === role).length })),
    [items],
  );

  function updateForm<K extends keyof UserForm>(key: K, value: UserForm[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function openEdit(user: LoginUser) {
    setEditing(user);
    setForm(toForm(user));
  }

  async function updateUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing) return;

    if (!form.username.trim() || !form.full_name.trim()) {
      toast.error({ message: "Username dan Full Name wajib diisi." });
      return;
    }

    if (form.password && form.password.length < 6) {
      toast.error({ message: "Password minimal 6 karakter." });
      return;
    }

    setBusy(true);
    try {
      await apiPut<LoginUser>(`/api/users/${editing.id}`, {
        username: form.username.trim(),
        full_name: form.full_name.trim(),
        email: form.email.trim() || null,
        phone: form.phone.trim() || null,
        role: form.role,
        status: form.status,
        password: form.password || null,
      });
      toast.success({ message: "User berhasil diupdate." });
      setEditing(null);
      setForm(emptyForm);
      await load();
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal update user." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      {editing ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-4">
          <form className="w-full max-w-4xl overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:bg-slate-900" onSubmit={(event) => void updateUser(event)}>
            <div className="flex items-center justify-between bg-[#0799c9] px-5 py-3 text-white">
              <div>
                <h2 className="text-sm font-black text-white">Update Login User</h2>
                <p className="mt-0.5 text-xs font-semibold text-cyan-50">@{editing.username}</p>
              </div>
              <button
                aria-label="Close update user modal"
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
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Username</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("username", event.target.value)} value={form.username} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Full Name</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("full_name", event.target.value)} value={form.full_name} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Email</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("email", event.target.value)} type="email" value={form.email} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Phone</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("phone", event.target.value)} value={form.phone} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Role</span>
                <select className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("role", event.target.value as LoginUserRole)} value={form.role}>
                  {roleOptions.map((role) => <option key={role} value={role}>{role}</option>)}
                </select>
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Status</span>
                <select className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("status", event.target.value as LoginUserStatus)} value={form.status}>
                  {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </label>
              <label className="block md:col-span-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">New Password</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateForm("password", event.target.value)} placeholder="Kosongkan jika tidak ganti password" type="password" value={form.password} />
              </label>
            </div>

            <div className="flex justify-end gap-3 border-t border-slate-100 px-5 py-4 dark:border-slate-800">
              <button className="h-10 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={() => setEditing(null)} type="button">
                Cancel
              </button>
              <button className="h-10 rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white hover:bg-[#087ea4] disabled:bg-[#0f5f78]" disabled={busy} type="submit">
                Save User
              </button>
            </div>
          </form>
        </div>
      ) : null}

      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Master Data</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Login User</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">User yang dapat login ke Production Control.</p>
      </div>

      <section className="grid gap-3 md:grid-cols-4">
        {roleSummary.map((item) => (
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" key={item.role}>
            <p className="text-xs font-bold text-slate-400">{item.role}</p>
            <p className="mt-2 text-2xl font-black text-slate-900 dark:text-white">{item.count}</p>
          </div>
        ))}
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">User List</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Update akses login, role, status, dan password user.</p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <input className={inputClass} onChange={(event) => setSearch(event.target.value)} placeholder="Search user" value={search} />
            <select className={inputClass} onChange={(event) => setStatusFilter(event.target.value as LoginUserStatus | "ALL")} value={statusFilter}>
              <option value="ALL">All Status</option>
              {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
            </select>
          </div>
        </div>
        <div className="overflow-x-auto p-5">
          <table className="w-full min-w-[1050px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">User</th>
                <th className="bg-[#0799c9] px-4 py-3">Contact</th>
                <th className="bg-[#0799c9] px-4 py-3">Role</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="bg-[#0799c9] px-4 py-3">Last Login</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {items.map((item) => (
                <tr className="hover:bg-slate-50 dark:hover:bg-slate-800/50" key={item.id}>
                  <td className="px-5 py-4">
                    <p className="text-sm font-black text-slate-900 dark:text-white">{item.full_name}</p>
                    <p className="mt-1 font-mono text-xs font-semibold text-slate-500 dark:text-slate-300">@{item.username}</p>
                  </td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">
                    <p>{item.email || "-"}</p>
                    <p className="mt-1">{item.phone || "-"}</p>
                  </td>
                  <td className="px-4 py-4">
                    <span className="rounded-full bg-cyan-50 px-2.5 py-1 text-xs font-bold text-cyan-700 dark:bg-cyan-500/10 dark:text-cyan-300">{item.role}</span>
                  </td>
                  <td className="px-4 py-4">
                    <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${statusClass(item.status)}`}>{item.status}</span>
                  </td>
                  <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{formatDateTime(item.last_login_at)}</td>
                  <td className="px-5 py-4 text-right">
                    <button className="h-9 rounded-md bg-[#0799c9] px-3 text-xs font-bold text-white hover:bg-[#087ea4]" onClick={() => openEdit(item)} type="button">
                      Update
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!items.length ? (
            <div className="flex min-h-[240px] items-center justify-center px-5 py-12">
              <p className="text-center text-sm text-slate-400 dark:text-slate-200">{loading ? "Loading users..." : "No login user data."}</p>
            </div>
          ) : null}
        </div>
      </section>
    </div>
  );
}
