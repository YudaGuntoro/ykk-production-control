"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, apiPost, apiPut } from "@/lib/api";
import type { LoginRole, LoginUser, LoginUserStatus } from "./types";
import { formatDateTime } from "./ui";

const inputClass =
  "h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-800 placeholder:text-slate-500 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-300";

const statusOptions: LoginUserStatus[] = ["ACTIVE", "INACTIVE"];
const adminRoleCode = "ADMIN";

type UserForm = {
  username: string;
  full_name: string;
  email: string;
  phone: string;
  role_id: string;
  status: LoginUserStatus;
  password: string;
};

type RoleForm = {
  role_code: string;
  role_name: string;
  description: string;
  is_active: boolean;
};

const emptyUserForm: UserForm = {
  username: "",
  full_name: "",
  email: "",
  phone: "",
  role_id: "",
  status: "ACTIVE",
  password: "",
};

const emptyRoleForm: RoleForm = {
  role_code: "",
  role_name: "",
  description: "",
  is_active: true,
};

function toForm(user: LoginUser): UserForm {
  return {
    username: user.username,
    full_name: user.full_name,
    email: user.email ?? "",
    phone: user.phone ?? "",
    role_id: user.role_id ? String(user.role_id) : "",
    status: user.status,
    password: "",
  };
}

function statusClass(status: LoginUserStatus) {
  return status === "ACTIVE"
    ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
    : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300";
}

function isAdminRole(role?: Pick<LoginRole, "role_code"> | null) {
  return role?.role_code?.toUpperCase() === adminRoleCode;
}

function isAdminUser(user?: LoginUser | null) {
  return user?.role?.toUpperCase() === adminRoleCode;
}

export default function UserManagementPage() {
  const toast = useToast();
  const [items, setItems] = useState<LoginUser[]>([]);
  const [roles, setRoles] = useState<LoginRole[]>([]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<LoginUserStatus | "ALL">("ALL");
  const [roleFilter, setRoleFilter] = useState("ALL");
  const [editing, setEditing] = useState<LoginUser | null>(null);
  const [isUserModalOpen, setIsUserModalOpen] = useState(false);
  const [isRoleModalOpen, setIsRoleModalOpen] = useState(false);
  const [userForm, setUserForm] = useState<UserForm>(emptyUserForm);
  const [roleForm, setRoleForm] = useState<RoleForm>(emptyRoleForm);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  const activeAssignableRoles = roles.filter((role) => role.is_active && !isAdminRole(role));
  const editableRoles = editing && isAdminUser(editing)
    ? roles.filter((role) => role.is_active && (role.id === editing.role_id || !isAdminRole(role)))
    : activeAssignableRoles;

  const loadRoles = useCallback(async () => {
    try {
      setRoles(await apiGet<LoginRole[]>("/api/users/roles"));
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal load role." });
    }
  }, [toast]);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "200" });
      if (search.trim()) params.set("search", search.trim());
      if (statusFilter !== "ALL") params.set("status", statusFilter);
      if (roleFilter !== "ALL") params.set("roleId", roleFilter);
      setItems(await apiGet<LoginUser[]>(`/api/users?${params.toString()}`));
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal load user." });
    } finally {
      setLoading(false);
    }
  }, [roleFilter, search, statusFilter, toast]);

  useEffect(() => {
    void loadRoles();
  }, [loadRoles]);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  const roleSummary = useMemo(
    () => roles.map((role) => ({ role, count: items.filter((item) => item.role_id === role.id).length })),
    [items, roles],
  );

  function updateUserForm<K extends keyof UserForm>(key: K, value: UserForm[K]) {
    setUserForm((current) => ({ ...current, [key]: value }));
  }

  function updateRoleForm<K extends keyof RoleForm>(key: K, value: RoleForm[K]) {
    setRoleForm((current) => ({ ...current, [key]: value }));
  }

  function openRegister() {
    setEditing(null);
    setUserForm({ ...emptyUserForm, role_id: activeAssignableRoles[0] ? String(activeAssignableRoles[0].id) : "" });
    setIsUserModalOpen(true);
  }

  function openEdit(user: LoginUser) {
    setEditing(user);
    setUserForm(toForm(user));
    setIsUserModalOpen(true);
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!userForm.username.trim() || !userForm.full_name.trim() || !userForm.role_id) {
      toast.error({ message: "Username, Full Name, dan Role wajib diisi." });
      return;
    }

    if (!editing && !userForm.password) {
      toast.error({ message: "Password wajib diisi saat register user." });
      return;
    }

    const selectedRole = roles.find((role) => String(role.id) === userForm.role_id);
    if (isAdminRole(selectedRole) && !isAdminUser(editing)) {
      toast.error({ message: "Role Admin tidak bisa dipilih untuk user baru." });
      return;
    }

    if (userForm.password && userForm.password.length < 6) {
      toast.error({ message: "Password minimal 6 karakter." });
      return;
    }

    setBusy(true);
    try {
      const payload = {
        username: userForm.username.trim(),
        full_name: userForm.full_name.trim(),
        email: userForm.email.trim() || null,
        phone: userForm.phone.trim() || null,
        role_id: Number(userForm.role_id),
        status: userForm.status,
        password: userForm.password || null,
      };

      if (editing) {
        await apiPut<LoginUser>(`/api/users/${editing.id}`, payload);
        toast.success({ message: "User berhasil diupdate." });
      } else {
        await apiPost<LoginUser>("/api/users", payload);
        toast.success({ message: "User berhasil diregister." });
      }

      setIsUserModalOpen(false);
      setEditing(null);
      setUserForm(emptyUserForm);
      await loadUsers();
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal simpan user." });
    } finally {
      setBusy(false);
    }
  }

  async function createRole(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!roleForm.role_code.trim() || !roleForm.role_name.trim()) {
      toast.error({ message: "Role Code dan Role Name wajib diisi." });
      return;
    }
    if (roleForm.role_code.trim().toUpperCase() === adminRoleCode) {
      toast.error({ message: "Role Admin adalah role sistem dan tidak bisa dibuat manual." });
      return;
    }

    setBusy(true);
    try {
      await apiPost<LoginRole>("/api/users/roles", {
        role_code: roleForm.role_code.trim(),
        role_name: roleForm.role_name.trim(),
        description: roleForm.description.trim() || null,
        is_active: roleForm.is_active,
      });
      toast.success({ message: "Role berhasil ditambahkan." });
      setRoleForm(emptyRoleForm);
      setIsRoleModalOpen(false);
      await loadRoles();
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal tambah role." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      {isUserModalOpen ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-4">
          <form className="w-full max-w-4xl overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:bg-slate-900" onSubmit={(event) => void saveUser(event)}>
            <div className="flex items-center justify-between bg-[#0799c9] px-5 py-3 text-white">
              <div>
                <h2 className="text-sm font-black text-white">{editing ? "Update Login User" : "Register Login User"}</h2>
                <p className="mt-0.5 text-xs font-semibold text-cyan-50">{editing ? `@${editing.username}` : "Masukkan detail user dan pilih role."}</p>
              </div>
              <button
                aria-label="Close user modal"
                className="flex h-8 w-8 items-center justify-center rounded-md text-sm font-black text-white/80 hover:bg-white/15 hover:text-white"
                disabled={busy}
                onClick={() => setIsUserModalOpen(false)}
                type="button"
              >
                X
              </button>
            </div>

            <div className="grid gap-4 p-5 md:grid-cols-2">
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Username</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("username", event.target.value)} value={userForm.username} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Full Name</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("full_name", event.target.value)} value={userForm.full_name} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Email</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("email", event.target.value)} type="email" value={userForm.email} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Phone</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("phone", event.target.value)} value={userForm.phone} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Role</span>
                <select className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("role_id", event.target.value)} value={userForm.role_id}>
                  <option value="">Pilih role</option>
                  {editableRoles.map((role) => <option key={role.id} value={role.id}>{role.role_name}</option>)}
                </select>
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Status</span>
                <select className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("status", event.target.value as LoginUserStatus)} value={userForm.status}>
                  {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </label>
              <label className="block md:col-span-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">{editing ? "New Password" : "Password"}</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateUserForm("password", event.target.value)} placeholder={editing ? "Kosongkan jika tidak ganti password" : "Minimal 6 karakter"} type="password" value={userForm.password} />
              </label>
            </div>

            <div className="flex justify-end gap-3 border-t border-slate-100 px-5 py-4 dark:border-slate-800">
              <button className="h-10 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={() => setIsUserModalOpen(false)} type="button">
                Cancel
              </button>
              <button className="h-10 rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white hover:bg-[#087ea4] disabled:bg-[#0f5f78]" disabled={busy} type="submit">
                {editing ? "Save User" : "Register User"}
              </button>
            </div>
          </form>
        </div>
      ) : null}

      {isRoleModalOpen ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-4">
          <form className="w-full max-w-2xl overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:bg-slate-900" onSubmit={(event) => void createRole(event)}>
            <div className="flex items-center justify-between bg-[#0799c9] px-5 py-3 text-white">
              <h2 className="text-sm font-black text-white">Tambah Role</h2>
              <button className="flex h-8 w-8 items-center justify-center rounded-md text-sm font-black text-white/80 hover:bg-white/15 hover:text-white" disabled={busy} onClick={() => setIsRoleModalOpen(false)} type="button">X</button>
            </div>
            <div className="grid gap-4 p-5 md:grid-cols-2">
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Role Code</span>
                <input className={`${inputClass} mt-2 uppercase`} disabled={busy} onChange={(event) => updateRoleForm("role_code", event.target.value)} placeholder="QA_LEADER" value={roleForm.role_code} />
              </label>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Role Name</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateRoleForm("role_name", event.target.value)} placeholder="QA Leader" value={roleForm.role_name} />
              </label>
              <label className="block md:col-span-2">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Description</span>
                <input className={`${inputClass} mt-2`} disabled={busy} onChange={(event) => updateRoleForm("description", event.target.value)} value={roleForm.description} />
              </label>
              <label className="flex items-center gap-2 rounded-md bg-slate-50 px-3 py-3 text-sm font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200 md:col-span-2">
                <input className="h-4 w-4 rounded border-slate-300 text-[#0799c9]" checked={roleForm.is_active} disabled={busy} onChange={(event) => updateRoleForm("is_active", event.target.checked)} type="checkbox" />
                Active
              </label>
            </div>
            <div className="flex justify-end gap-3 border-t border-slate-100 px-5 py-4 dark:border-slate-800">
              <button className="h-10 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={() => setIsRoleModalOpen(false)} type="button">Cancel</button>
              <button className="h-10 rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white hover:bg-[#087ea4] disabled:bg-[#0f5f78]" disabled={busy} type="submit">Save Role</button>
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
        {roleSummary.filter((item) => !isAdminRole(item.role)).map((item) => (
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" key={item.role.id}>
            <p className="text-xs font-bold text-slate-400">{item.role.role_name}</p>
            <p className="mt-2 text-2xl font-black text-slate-900 dark:text-white">{item.count}</p>
          </div>
        ))}
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">User List</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Register user, tambah role, dan update akses login.</p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <input className={inputClass} onChange={(event) => setSearch(event.target.value)} placeholder="Search user" value={search} />
            <select className={inputClass} onChange={(event) => setRoleFilter(event.target.value)} value={roleFilter}>
              <option value="ALL">All Role</option>
              {roles.filter((role) => !isAdminRole(role)).map((role) => <option key={role.id} value={role.id}>{role.role_name}</option>)}
            </select>
            <select className={inputClass} onChange={(event) => setStatusFilter(event.target.value as LoginUserStatus | "ALL")} value={statusFilter}>
              <option value="ALL">All Status</option>
              {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
            </select>
            <button className="h-11 rounded-md border border-slate-200 px-4 text-sm font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" onClick={() => setIsRoleModalOpen(true)} type="button">
              Add Role
            </button>
            <button className="h-11 rounded-md bg-[#0799c9] px-4 text-sm font-bold text-white hover:bg-[#087ea4]" onClick={openRegister} type="button">
              Register
            </button>
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
                    <span className="rounded-full bg-cyan-50 px-2.5 py-1 text-xs font-bold text-cyan-700 dark:bg-cyan-500/10 dark:text-cyan-300">{item.role_name || item.role}</span>
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
