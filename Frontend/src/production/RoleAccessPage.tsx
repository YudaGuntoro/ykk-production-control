"use client";

import { useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, apiPut } from "@/lib/api";
import type { LoginRole, RolePageAccess } from "./types";

const adminRoleCode = "ADMIN";

export default function RoleAccessPage() {
  const toast = useToast();
  const [roles, setRoles] = useState<LoginRole[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState("");
  const [pages, setPages] = useState<RolePageAccess[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  const selectedRole = useMemo(
    () => roles.find((role) => String(role.id) === selectedRoleId) ?? null,
    [roles, selectedRoleId],
  );
  const configurableRoles = roles.filter((role) => role.role_code?.toUpperCase() !== adminRoleCode);
  const isAdminRole = selectedRole?.role_code?.toUpperCase() === adminRoleCode;
  const groupedPages = useMemo(() => {
    return pages.reduce<Record<string, RolePageAccess[]>>((groups, page) => {
      groups[page.group_name] = [...(groups[page.group_name] ?? []), page];
      return groups;
    }, {});
  }, [pages]);

  useEffect(() => {
    let alive = true;

    async function loadRoles() {
      setLoading(true);
      try {
        const rows = await apiGet<LoginRole[]>("/api/users/roles?isActive=true");
        if (!alive) return;
        setRoles(rows);
        const firstConfigurableRole = rows.find((role) => role.role_code?.toUpperCase() !== adminRoleCode);
        setSelectedRoleId(firstConfigurableRole ? String(firstConfigurableRole.id) : "");
      } catch (error) {
        toast.error({ message: error instanceof Error ? error.message : "Gagal load role." });
      } finally {
        if (alive) setLoading(false);
      }
    }

    void loadRoles();
    return () => {
      alive = false;
    };
  }, [toast]);

  useEffect(() => {
    if (!selectedRoleId) {
      setPages([]);
      return;
    }

    let alive = true;
    async function loadAccess() {
      setLoading(true);
      try {
        const rows = await apiGet<RolePageAccess[]>(`/api/users/roles/${selectedRoleId}/page-access`);
        if (alive) setPages(rows);
      } catch (error) {
        toast.error({ message: error instanceof Error ? error.message : "Gagal load akses role." });
      } finally {
        if (alive) setLoading(false);
      }
    }

    void loadAccess();
    return () => {
      alive = false;
    };
  }, [selectedRoleId, toast]);

  function togglePage(pageKey: string, checked: boolean) {
    setPages((current) =>
      current.map((page) => page.page_key === pageKey ? { ...page, can_access: checked } : page),
    );
  }

  async function saveAccess() {
    if (!selectedRoleId || isAdminRole) {
      return;
    }

    setBusy(true);
    try {
      await apiPut<RolePageAccess[]>(`/api/users/roles/${selectedRoleId}/page-access`, {
        page_keys: pages.filter((page) => page.can_access).map((page) => page.page_key),
      });
      toast.success({ message: "Akses page berhasil disimpan." });
    } catch (error) {
      toast.error({ message: error instanceof Error ? error.message : "Gagal simpan akses page." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Master Data</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Role Access</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">Atur page mana yang bisa diakses oleh setiap role. Admin otomatis bisa semua page.</p>
      </div>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-4 border-b border-slate-100 px-5 py-4 dark:border-slate-800 md:flex-row md:items-end md:justify-between">
          <label className="block w-full md:max-w-sm">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Role</span>
            <select
              className="mt-2 h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm font-bold text-slate-800 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-50"
              disabled={loading || busy}
              onChange={(event) => setSelectedRoleId(event.target.value)}
              value={selectedRoleId}
            >
              {configurableRoles.map((role) => (
                <option key={role.id} value={role.id}>{role.role_name}</option>
              ))}
            </select>
          </label>
          <button
            className="h-11 rounded-md bg-[#0799c9] px-5 text-sm font-bold text-white hover:bg-[#087ea4] disabled:cursor-not-allowed disabled:bg-[#0f5f78] disabled:text-cyan-50"
            disabled={loading || busy || isAdminRole}
            onClick={() => void saveAccess()}
            type="button"
          >
            {busy ? "Saving..." : "Save Access"}
          </button>
        </div>

        {isAdminRole ? (
          <div className="mx-5 mt-5 rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm font-semibold text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-200">
            Admin role otomatis memiliki akses ke semua page dan tidak perlu diatur.
          </div>
        ) : null}

        <div className="grid gap-4 p-5 lg:grid-cols-2">
          {Object.entries(groupedPages).map(([groupName, groupPages]) => (
            <div className="rounded-lg border border-slate-200 p-4 dark:border-slate-800" key={groupName}>
              <h2 className="text-sm font-black text-slate-900 dark:text-white">{groupName}</h2>
              <div className="mt-4 space-y-2">
                {groupPages.map((page) => (
                  <label
                    className="flex items-start gap-3 rounded-md bg-slate-50 px-3 py-3 text-sm dark:bg-slate-950"
                    key={page.page_key}
                  >
                    <input
                      checked={isAdminRole || page.can_access}
                      className="mt-0.5 h-4 w-4 rounded border-slate-300 text-[#0799c9]"
                      disabled={loading || busy || isAdminRole}
                      onChange={(event) => togglePage(page.page_key, event.target.checked)}
                      type="checkbox"
                    />
                    <span>
                      <span className="block font-bold text-slate-800 dark:text-slate-100">{page.page_name}</span>
                      <span className="mt-1 block font-mono text-xs text-slate-400">{page.path}</span>
                    </span>
                  </label>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
