"use client";

import { FormEvent, KeyboardEvent, useCallback, useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, apiPost } from "@/lib/api";
import type { ActiveOperatorSummary, AreaMaster, ProductionActiveOperator, ProductionWorkOrder, ProductionWorkOrderStatus } from "./types";
import { formatDateTime, StatusBadge, statusStyles, useDebouncedValue } from "./ui";

const inputClass =
  "h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-800 placeholder:text-slate-500 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 disabled:cursor-not-allowed disabled:opacity-100 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-300 disabled:dark:text-slate-300";
const scanButtonClass =
  "h-11 w-full shrink-0 rounded-md bg-[#0799c9] px-4 text-sm font-bold leading-tight text-white transition hover:bg-[#087ea4] disabled:bg-[#0f5f78] disabled:text-cyan-50 disabled:opacity-100 sm:w-[122px]";

const activeStatuses = new Set(["WAITING", "IN_PROGRESS"]);
const statusOptions: Array<ProductionWorkOrderStatus | "ALL"> = ["ALL", "WAITING", "IN_PROGRESS", "FINISH"];

function isActiveOrder(order: ProductionWorkOrder) {
  return activeStatuses.has(order.status);
}

function actualText(order?: ProductionWorkOrder | null) {
  return order?.completed_at ? order.actual_qty.toLocaleString("en-US") : "-";
}

function rejectText(order?: ProductionWorkOrder | null) {
  return order?.completed_at ? order.reject_qty.toLocaleString("en-US") : "-";
}

function shiftText(summary?: ActiveOperatorSummary | null) {
  const shift = summary?.current_shift;
  if (!shift) return "-";
  return [shift.shift_name, shift.shift_type].filter(Boolean).join(" / ");
}

function statusText(status: ProductionWorkOrderStatus) {
  return status.replaceAll("_", " ");
}

export default function ProductionControlPage() {
  const toast = useToast();
  const [orders, setOrders] = useState<ProductionWorkOrder[]>([]);
  const [activeSummary, setActiveSummary] = useState<ActiveOperatorSummary | null>(null);
  const [areaMasters, setAreaMasters] = useState<AreaMaster[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [selectedAreaMasterId, setSelectedAreaMasterId] = useState("");
  const [statusFilter, setStatusFilter] = useState<ProductionWorkOrderStatus | "ALL">("ALL");
  const [orderNumberFilter, setOrderNumberFilter] = useState("");
  const [orderNumberCode, setOrderNumberCode] = useState("");
  const [operatorCardUid, setOperatorCardUid] = useState("");
  const [busy, setBusy] = useState(false);
  const [shiftPromptKey, setShiftPromptKey] = useState<string | null>(null);
  const [confirmRemoveShiftOperators, setConfirmRemoveShiftOperators] = useState(false);
  const [operatorPendingRemove, setOperatorPendingRemove] = useState<ProductionActiveOperator | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [cancelFinishReason, setCancelFinishReason] = useState("");
  const [cancelFinishModalOpen, setCancelFinishModalOpen] = useState(false);

  const load = useCallback(async (preferredId?: number, nextStatusFilter = statusFilter) => {
    try {
      const query = nextStatusFilter === "ALL" ? "" : `?status=${nextStatusFilter}`;
      const workOrders = await apiGet<ProductionWorkOrder[]>(`/api/production/work-orders${query}`);
      setOrders(workOrders);
      setSelectedId((current) => {
        if (preferredId && workOrders.some((order) => order.id === preferredId)) {
          return preferredId;
        }

        if (current && workOrders.some((order) => order.id === current)) {
          return current;
        }

        return workOrders.find(isActiveOrder)?.id ?? workOrders[0]?.id ?? null;
      });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to load data." });
    }
  }, [statusFilter, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const loadActiveOperators = useCallback(async () => {
    try {
      setActiveSummary(await apiGet<ActiveOperatorSummary>("/api/production/active-operators"));
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to load active operators." });
    }
  }, [toast]);

  const loadAreaMasters = useCallback(async () => {
    try {
      const areas = await apiGet<AreaMaster[]>("/api/production/line-master?page=1&pageSize=100&isActive=true");
      const sortedAreas = [...areas].sort((left, right) => left.id - right.id);
      setAreaMasters(sortedAreas);
      setSelectedAreaMasterId(sortedAreas[0] ? String(sortedAreas[0].id) : "");
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to load line master." });
    }
  }, [toast]);

  useEffect(() => {
    void loadActiveOperators();
    void loadAreaMasters();
  }, [loadActiveOperators, loadAreaMasters]);

  const selected = useMemo(
    () => orders.find((order) => order.id === selectedId) ?? null,
    [orders, selectedId],
  );

  const activeOperators = activeSummary?.operators ?? [];
  const activeOperatorIds = activeOperators.map((operator) => operator.id).join("-");
  const activeOrders = useMemo(() => orders.filter(isActiveOrder), [orders]);
  const selectedAreaMaster = useMemo(
    () => areaMasters.find((area) => String(area.id) === selectedAreaMasterId) ?? null,
    [areaMasters, selectedAreaMasterId],
  );
  const selectedAreaText = selectedAreaMaster
    ? [selectedAreaMaster.line_no, selectedAreaMaster.line_name].filter(Boolean).join(" - ")
    : "";
  const debouncedOrderNumberFilter = useDebouncedValue(orderNumberFilter, 300);
  const visibleOrders = useMemo(() => {
    const keyword = debouncedOrderNumberFilter.trim().toLowerCase();
    if (!keyword) {
      return orders;
    }

    return orders.filter((order) =>
      (order.lot_no || "").toLowerCase().includes(keyword) ||
      order.order_number.toLowerCase().includes(keyword),
    );
  }, [debouncedOrderNumberFilter, orders]);
  const runningOrders = useMemo(
    () => activeOrders.filter((order) => order.status === "IN_PROGRESS"),
    [activeOrders],
  );

  useEffect(() => {
    if (!activeSummary?.has_shift_changed || !activeSummary.current_shift || !activeOperators.length) {
      setShiftPromptKey(null);
      setConfirmRemoveShiftOperators(false);
      return;
    }

    const key = `production-shift-decision:${activeSummary.current_shift.shift_code}:${activeOperatorIds}`;
    if (typeof window !== "undefined" && window.localStorage.getItem(key) === "done") {
      return;
    }

    setShiftPromptKey(key);
  }, [activeSummary, activeOperatorIds, activeOperators.length]);

  async function refreshFromResponse(order: ProductionWorkOrder, text: string, nextStatusFilter = statusFilter) {
    setSelectedId(order.id);
    toast.success({ message: text });
    await load(order.id, nextStatusFilter);
  }

  async function scanOrderNumber(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const code = orderNumberCode.trim();
    if (!code) {
      toast.error({ message: "Input Lot No first." });
      return;
    }

    setBusy(true);
    try {
      const order = await apiPost<ProductionWorkOrder>("/api/production/work-orders/scan", { lot_no: code });
      setOrderNumberCode("");
      setStatusFilter("ALL");
      await refreshFromResponse(order, "Active order selected.", "ALL");
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to load Lot No." });
    } finally {
      setBusy(false);
    }
  }

  function submitLotScanOnEnter(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Enter") {
      event.preventDefault();
      event.currentTarget.form?.requestSubmit();
    }
  }

  async function scanOperator(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const cardUid = operatorCardUid.trim();
    if (!cardUid) {
      toast.error({ message: "Scan the operator ID first." });
      return;
    }

    setBusy(true);
    try {
      const summary = await apiPost<ActiveOperatorSummary>("/api/production/active-operators/scan", { card_uid: cardUid });
      setOperatorCardUid("");
      setActiveSummary(summary);
      toast.success({ message: "Operator is active and will be used for the next started Order Number." });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to scan operator." });
    } finally {
      setBusy(false);
    }
  }

  async function removeOperator(operatorId: number) {
    setBusy(true);
    try {
      const summary = await apiPost<ActiveOperatorSummary>(`/api/production/active-operators/${operatorId}/remove`);
      setActiveSummary(summary);
      setOperatorPendingRemove(null);
      toast.success({ message: "Operator removed from active list." });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to remove operator." });
    } finally {
      setBusy(false);
    }
  }

  async function action(path: string, body?: unknown, success = "Updated successfully.") {
    if (!selected) return;
    setBusy(true);
    try {
      const order = await apiPost<ProductionWorkOrder>(`/api/production/work-orders/${selected.id}/${path}`, body);
      await refreshFromResponse(order, success);
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Process failed." });
    } finally {
      setBusy(false);
    }
  }

  function selectOrder(orderId: number) {
    setSelectedId(orderId);
  }

  function openDetail(orderId: number) {
    setSelectedId(orderId);
    setDetailOpen(true);
  }

  function selectOrderFromKeyboard(event: KeyboardEvent<HTMLTableRowElement>, orderId: number) {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      selectOrder(orderId);
    }
  }

  const selectedOperators = selected?.operators ?? [];
  const canScanOperator = true;
  const canStart = Boolean(selected && selected.status === "WAITING" && activeOperators.length > 0 && selectedAreaMasterId);
  const canFinish = selected?.status === "IN_PROGRESS";
  const canCancelFinish = selected?.status === "FINISH" && Boolean(selected.completed_at);

  useEffect(() => {
    if (detailOpen && !selected) {
      setDetailOpen(false);
    }
  }, [detailOpen, selected]);

  function startBlockedReason() {
    if (!selected) return "Pilih Order Number terlebih dahulu.";
    if (activeOperators.length < 1) return "Tidak boleh Start karena belum ada operator aktif. Scan minimal 1 operator terlebih dahulu.";
    if (!selectedAreaMasterId) return "Line kerja belum tersedia. Tambahkan Line Master terlebih dahulu.";
    if (selected.status === "IN_PROGRESS") return "Tidak boleh Start karena Order Number sudah berjalan.";
    if (selected.status === "FINISH") return "Tidak boleh Start karena Order Number sudah finish.";
    return null;
  }

  function finishBlockedReason() {
    if (!selected) return "Pilih Order Number terlebih dahulu.";
    if (selected.status === "FINISH") return "Tidak boleh Finish karena Order Number sudah finish.";
    return "Tidak boleh Finish karena Order Number belum Start / belum IN PROGRESS.";
  }

  function cancelFinishBlockedReason() {
    if (!selected) return "Pilih Order Number terlebih dahulu.";
    if (selected.status !== "FINISH" || !selected.completed_at) {
      return "Tidak boleh Cancel Finish karena Order Number belum finish.";
    }

    return null;
  }

  function showBlockedAction(reason: string | null) {
    if (!reason) {
      return false;
    }

    toast.error({ message: reason });
    return true;
  }

  async function submitCancelFinish(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const reason = cancelFinishReason.trim();

    if (!reason) {
      toast.error({ message: "Alasan cancel wajib diisi." });
      return;
    }

    await action("cancel-finish", { remarks: reason }, "Finish canceled.");
    setCancelFinishReason("");
    setCancelFinishModalOpen(false);
  }

  function keepShiftOperators() {
    if (shiftPromptKey && typeof window !== "undefined") {
      window.localStorage.setItem(shiftPromptKey, "done");
    }
    setShiftPromptKey(null);
    setConfirmRemoveShiftOperators(false);
  }

  async function removeAllShiftOperators() {
    setBusy(true);
    try {
      const summary = await apiPost<ActiveOperatorSummary>("/api/production/active-operators/remove-all");
      if (shiftPromptKey && typeof window !== "undefined") {
        window.localStorage.setItem(shiftPromptKey, "done");
      }
      setActiveSummary(summary);
      setShiftPromptKey(null);
      setConfirmRemoveShiftOperators(false);
      toast.success({ message: "All active operators removed. Scan the next team when ready." });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to remove active operators." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      {operatorPendingRemove ? (
        <div className="fixed inset-0 z-[100010] flex items-center justify-center bg-slate-950/50 px-4">
          <div className="w-full max-w-md rounded-lg bg-white p-5 shadow-xl dark:bg-slate-900">
            <h2 className="text-lg font-black text-slate-900 dark:text-white">Anda yakin?</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
              Yakin untuk remove <span className="font-bold">{operatorPendingRemove.full_name}</span> dari daftar Operator Aktif?
            </p>
            <p className="mt-2 text-xs text-slate-400 dark:text-slate-300">
              {operatorPendingRemove.employee_no} / {operatorPendingRemove.shift_name || "-"} / {operatorPendingRemove.shift_type || "-"}
            </p>
            <div className="mt-5 grid grid-cols-2 gap-3">
              <button
                className="h-10 rounded-md bg-rose-600 text-xs font-bold text-white hover:bg-rose-700 disabled:bg-rose-900 disabled:text-rose-50 disabled:opacity-100"
                disabled={busy}
                onClick={() => void removeOperator(operatorPendingRemove.id)}
                type="button"
              >
                Ya, Remove
              </button>
              <button
                className="h-10 rounded-md border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200"
                disabled={busy}
                onClick={() => setOperatorPendingRemove(null)}
                type="button"
              >
                Batal
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {cancelFinishModalOpen ? (
        <div className="fixed inset-0 z-[100010] flex items-center justify-center bg-slate-950/50 px-4">
          <form className="w-full max-w-md rounded-lg bg-white p-5 shadow-xl dark:bg-slate-900" onSubmit={(event) => void submitCancelFinish(event)}>
            <h2 className="text-lg font-black text-slate-900 dark:text-white">Cancel Finish</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Masukkan alasan sebelum membatalkan finish order.</p>
            <label className="mt-5 block">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Reason</span>
              <textarea
                autoFocus
                className="mt-2 min-h-28 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-500"
                onChange={(event) => setCancelFinishReason(event.target.value)}
                placeholder="Silahkan input alasan cancel"
                value={cancelFinishReason}
              />
            </label>
            <div className="mt-5 grid grid-cols-2 gap-3">
              <button
                className="h-10 rounded-md bg-rose-600 text-xs font-bold text-white hover:bg-rose-700 disabled:bg-rose-900 disabled:text-rose-50 disabled:opacity-100"
                disabled={busy}
                type="submit"
              >
                Submit
              </button>
              <button
                className="h-10 rounded-md border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200"
                disabled={busy}
                onClick={() => {
                  setCancelFinishModalOpen(false);
                  setCancelFinishReason("");
                }}
                type="button"
              >
                Batal
              </button>
            </div>
          </form>
        </div>
      ) : null}

      {shiftPromptKey ? (
        <div className="fixed inset-0 z-[100010] flex items-center justify-center bg-slate-950/50 px-4">
          <div className="w-full max-w-md rounded-lg bg-white p-5 shadow-xl dark:bg-slate-900">
            {!confirmRemoveShiftOperators ? (
              <>
                <h2 className="text-lg font-black text-slate-900 dark:text-white">Waktu shift sudah berganti</h2>
                <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
                  Current shift: <span className="font-bold">{shiftText(activeSummary)}</span>. Apakah ingin mempertahankan daftar operator yang sedang aktif?
                </p>
                <div className="mt-5 grid grid-cols-2 gap-3">
                  <button className="h-10 rounded-md bg-[#0799c9] text-xs font-bold text-white hover:bg-[#087ea4]" onClick={keepShiftOperators} type="button">
                    Pertahankan Operator
                  </button>
                  <button className="h-10 rounded-md border border-rose-200 text-xs font-bold text-rose-600 hover:bg-rose-50 dark:border-rose-500/40 dark:text-rose-300" onClick={() => setConfirmRemoveShiftOperators(true)} type="button">
                    Remove Operator
                  </button>
                </div>
              </>
            ) : (
              <>
                <h2 className="text-lg font-black text-slate-900 dark:text-white">Anda yakin?</h2>
                <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Semua operator aktif akan dihapus dari daftar aktif. Riwayat operator pada WO yang sudah berjalan tetap tersimpan.</p>
                <div className="mt-5 grid grid-cols-2 gap-3">
                  <button className="h-10 rounded-md bg-rose-600 text-xs font-bold text-white hover:bg-rose-700 disabled:bg-rose-900 disabled:text-rose-50 disabled:opacity-100" disabled={busy} onClick={() => void removeAllShiftOperators()} type="button">
                    Ya, Remove
                  </button>
                  <button
                    className="h-10 rounded-md border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200"
                    disabled={busy}
                    onClick={() => {
                      setConfirmRemoveShiftOperators(false);
                      if (shiftPromptKey === "manual-remove-active-operators") {
                        setShiftPromptKey(null);
                      }
                    }}
                    type="button"
                  >
                    Batal
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      ) : null}

      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Operation</p>
          <h1 className="mt-2 text-2xl font-black leading-tight text-slate-900 dark:text-white">Production Control Panel</h1>
          <p className="mt-1 max-w-2xl text-sm leading-6 text-slate-500 dark:text-slate-300">Scan operators once, then use the active team for every started order until they are removed.</p>
        </div>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="grid gap-0 divide-y divide-slate-100 dark:divide-slate-800 xl:grid-cols-[1fr_1fr_0.8fr] xl:divide-x xl:divide-y-0">
          <form className="p-4 sm:p-5" onSubmit={(event) => void scanOrderNumber(event)}>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Lot No</label>
            <div className="mt-3 flex flex-col gap-3 sm:flex-row">
              <input
                autoFocus
                className={inputClass}
                enterKeyHint="done"
                onKeyDown={submitLotScanOnEnter}
                onChange={(event) => setOrderNumberCode(event.target.value)}
                placeholder="Scan / type Lot No"
                value={orderNumberCode}
              />
              <button className={scanButtonClass} disabled={busy} type="submit">
                Scan Lot
              </button>
            </div>
          </form>

          <form className="p-4 sm:p-5" onSubmit={(event) => void scanOperator(event)}>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Scan Operator Aktif</label>
            <div className="mt-3 flex flex-col gap-3 sm:flex-row">
              <input
                className={inputClass}
                disabled={!canScanOperator || busy}
                onChange={(event) => setOperatorCardUid(event.target.value)}
                placeholder="Scan card UID"
                value={operatorCardUid}
              />
              <button className={scanButtonClass} disabled={!canScanOperator || busy} type="submit">
                Scan Operator
              </button>
            </div>
          </form>

          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            <div className="p-4 sm:p-5">
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Line Area</label>
              <input
                className={`${inputClass} mt-3`}
                disabled
                placeholder="Line belum tersedia"
                value={selectedAreaText}
              />
              <p className="mt-2 text-xs text-slate-400 dark:text-slate-300">Dipakai saat Start Order Number.</p>
            </div>
            <div className="grid grid-cols-3 divide-x divide-slate-100 dark:divide-slate-800">
              <div className="p-3 sm:p-5">
                <p className="text-[11px] font-semibold leading-tight text-slate-500 dark:text-slate-300 sm:text-xs">Active Orders</p>
                <p className="mt-2 text-2xl font-black text-slate-900 dark:text-white">{activeOrders.length}</p>
              </div>
              <div className="p-3 sm:p-5">
                <p className="text-[11px] font-semibold leading-tight text-slate-500 dark:text-slate-300 sm:text-xs">Running in Parallel</p>
                <p className="mt-2 text-2xl font-black text-slate-900 dark:text-white">{runningOrders.length}</p>
              </div>
              <div className="p-3 sm:p-5">
                <p className="text-[11px] font-semibold leading-tight text-slate-500 dark:text-slate-300 sm:text-xs">Active Operators</p>
                <p className="mt-2 text-2xl font-black text-slate-900 dark:text-white">{activeOperators.length}</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900 sm:p-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">Operator Aktif</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Current shift: {shiftText(activeSummary)}</p>
          </div>
          <button
            className="h-9 w-full rounded-md border border-rose-200 px-3 text-xs font-bold text-rose-600 hover:bg-rose-50 disabled:opacity-40 dark:border-rose-500/30 dark:text-rose-300 sm:w-auto"
            disabled={!activeOperators.length || busy}
            onClick={() => {
              setShiftPromptKey("manual-remove-active-operators");
              setConfirmRemoveShiftOperators(true);
            }}
            type="button"
          >
            Remove All
          </button>
        </div>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {activeOperators.length ? activeOperators.map((operator) => (
            <div className="rounded-md bg-slate-50 p-3 dark:bg-slate-800" key={operator.id}>
              <div className="flex flex-col gap-3 min-[420px]:flex-row min-[420px]:items-start min-[420px]:justify-between">
                <div className="min-w-0">
                  <p className="truncate text-sm font-black text-slate-900 dark:text-white">{operator.full_name}</p>
                  <p className="mt-1 text-xs text-slate-500 dark:text-slate-300">{operator.employee_no} / {operator.department}</p>
                  <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">{operator.shift_name || "-"} / {operator.shift_type || "-"}</p>
                </div>
                <button className="h-8 shrink-0 rounded-md border border-rose-200 px-2 text-xs font-bold text-rose-600 hover:bg-rose-50 disabled:opacity-40 dark:border-rose-500/30 dark:text-rose-300 min-[420px]:w-auto" disabled={busy} onClick={() => setOperatorPendingRemove(operator)} type="button">
                  Remove
                </button>
              </div>
            </div>
          )) : (
            <p className="rounded-md bg-slate-50 px-3 py-4 text-sm font-semibold text-slate-400 dark:bg-slate-800 dark:text-slate-200">No active operators.</p>
          )}
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-3 border-b border-slate-100 px-4 py-4 dark:border-slate-800 sm:px-5 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">Work Orders</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Sorted by latest updated time, newest first.</p>
          </div>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">
              Lot No / Order Number
              <div className="mt-2 flex gap-2">
                <input
                  className="h-9 w-full rounded-md border border-slate-200 bg-white px-3 text-xs font-bold text-slate-700 outline-none placeholder:text-slate-400 focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500 lg:w-[280px]"
                  onChange={(event) => setOrderNumberFilter(event.target.value)}
                  placeholder="Scan / type Lot No or order number"
                  value={orderNumberFilter}
                />
                {orderNumberFilter ? (
                  <button
                    className="h-9 w-9 rounded-md border border-slate-200 text-xs font-black text-slate-500 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-300"
                    onClick={() => setOrderNumberFilter("")}
                    type="button"
                    aria-label="Clear Lot No filter"
                  >
                    X
                  </button>
                ) : null}
              </div>
            </label>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">
              Status
              <select
                className="mt-2 h-9 w-full rounded-md border border-slate-200 bg-white px-3 text-xs font-bold text-slate-700 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 lg:w-auto"
                onChange={(event) => setStatusFilter(event.target.value as ProductionWorkOrderStatus | "ALL")}
                value={statusFilter}
              >
                {statusOptions.map((status) => (
                  <option key={status} value={status}>{status === "ALL" ? "ALL STATUS" : status.replaceAll("_", " ")}</option>
                ))}
              </select>
            </label>
          </div>
        </div>
        <div className="hidden overflow-x-auto p-5 md:block">
          <table className="w-full min-w-[760px] border-separate border-spacing-0 text-left">
            <thead className="text-[11px] uppercase tracking-wider text-white">
              <tr>
                <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Lot No / Order Number</th>
                <th className="bg-[#0799c9] px-4 py-3">Line</th>
                <th className="bg-[#0799c9] px-4 py-3">Operators</th>
                <th className="bg-[#0799c9] px-4 py-3">Status</th>
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {visibleOrders.map((order) => {
                const selectedRow = selectedId === order.id;
                return (
                  <tr
                    aria-selected={selectedRow}
                    className={`cursor-pointer border-l-4 text-sm outline-none transition focus-visible:bg-cyan-100 dark:focus-visible:bg-cyan-500/20 ${selectedRow ? "border-l-[#0799c9] bg-cyan-100 dark:bg-cyan-500/20" : "border-l-transparent hover:bg-slate-50 dark:hover:bg-slate-800/50"}`}
                    key={order.id}
                    onClick={() => selectOrder(order.id)}
                    onKeyDown={(event) => selectOrderFromKeyboard(event, order.id)}
                    tabIndex={0}
                  >
                    <td className="px-5 py-4">
                      <p className="font-bold text-slate-900 dark:text-white">{order.lot_no || "-"}</p>
                      <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">{order.order_number}</p>
                    </td>
                    <td className="px-4 py-4 text-xs font-semibold text-slate-500 dark:text-slate-300">{order.line_name || "-"}</td>
                    <td className="px-4 py-4">
                      <span className="rounded-md bg-slate-100 px-2.5 py-1 text-xs font-bold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                        {order.operators?.length ?? 0} operator
                      </span>
                    </td>
                    <td className="px-4 py-4"><StatusBadge status={order.status} /></td>
                    <td className="px-5 py-4 text-right">
                      <button className="h-9 rounded-md border border-slate-200 px-3 text-xs font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200" onClick={(event) => { event.stopPropagation(); openDetail(order.id); }} type="button">
                        Detail
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {!visibleOrders.length ? <p className="px-5 py-12 text-center text-sm text-slate-400 dark:text-slate-200">No work orders found for the selected status.</p> : null}
        </div>
        <div className="space-y-3 p-4 md:hidden">
          {visibleOrders.map((order) => {
            const selectedRow = selectedId === order.id;
            return (
              <article
                aria-selected={selectedRow}
                className={`rounded-lg border p-4 transition ${selectedRow ? "border-[#0799c9] bg-cyan-50 dark:bg-cyan-500/10" : "border-slate-200 bg-slate-50 dark:border-slate-800 dark:bg-slate-950"}`}
                key={order.id}
              >
                <button
                  className="block w-full text-left"
                  onClick={() => selectOrder(order.id)}
                  type="button"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="break-words text-sm font-black text-slate-900 dark:text-white">{order.lot_no || "-"}</p>
                      <p className="mt-1 break-words text-xs font-semibold text-slate-400 dark:text-slate-300">{order.order_number}</p>
                    </div>
                    <StatusBadge status={order.status} />
                  </div>
                  <dl className="mt-4 grid grid-cols-1 gap-3 text-sm min-[420px]:grid-cols-2">
                    <div>
                      <dt className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Line</dt>
                      <dd className="mt-1 break-words font-semibold text-slate-700 dark:text-slate-200">{order.line_name || "-"}</dd>
                    </div>
                    <div>
                      <dt className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Operators</dt>
                      <dd className="mt-1 font-semibold text-slate-700 dark:text-slate-200">{order.operators?.length ?? 0} operator</dd>
                    </div>
                  </dl>
                </button>
                <button
                  className="mt-4 h-9 w-full rounded-md border border-slate-200 px-3 text-xs font-bold text-slate-700 hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200"
                  onClick={() => openDetail(order.id)}
                  type="button"
                >
                  Detail
                </button>
              </article>
            );
          })}
          {!visibleOrders.length ? <p className="px-5 py-12 text-center text-sm text-slate-400 dark:text-slate-200">No work orders found for the selected status.</p> : null}
        </div>
      </section>

      {detailOpen && selected ? (
        <div className="fixed inset-0 z-[100000] flex items-center justify-center bg-slate-950/50 p-6">
          <section className="flex max-h-[calc(100vh-3rem)] min-h-0 w-full max-w-6xl flex-col overflow-hidden rounded-lg border border-[#0799c9] bg-white shadow-xl dark:border-[#0799c9] dark:bg-slate-900">
            <div className="flex shrink-0 flex-col gap-1.5 bg-[#0799c9] px-4 py-2.5 text-white sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-xs font-bold text-white">Control {selected.order_number}</h2>
                <p className="mt-0.5 text-[10px] font-semibold text-cyan-50">{selected.lot_no || "-"}</p>
              </div>
              <button className="flex h-6 w-6 items-center justify-center rounded-md text-[11px] font-black text-white/80 hover:bg-white/15 hover:text-white" onClick={() => setDetailOpen(false)} type="button" aria-label="Close detail">
                X
              </button>
            </div>

            <div className="min-h-0 space-y-4 overflow-y-auto overscroll-contain p-5">
            <div className={`rounded-md px-4 py-6 text-center ring-1 ring-inset ${statusStyles[selected.status]}`}>
              <p className="text-2xl font-black uppercase">{statusText(selected.status)}</p>
            </div>

            <div className="grid gap-4 xl:grid-cols-[1.25fr_1fr]">
            <div className="rounded-md border border-slate-200 p-4 dark:border-slate-700">
              <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Detail Work Order</p>
              <div className="mt-4 grid gap-4 text-sm sm:grid-cols-2 xl:grid-cols-3">
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-300">Order Number</p>
                  <p className="mt-1 font-black text-slate-900 dark:text-white">{selected.order_number}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-300">Line</p>
                  <p className="mt-1 font-black text-slate-900 dark:text-white">{selected.line_code}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-300">Line</p>
                  <p className="mt-1 font-black text-slate-900 dark:text-white">{selected.line_name || "-"}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-300">Work Shift</p>
                  <p className="mt-1 font-black text-slate-900 dark:text-white">{[selected.work_shift_name, selected.work_shift_type].filter(Boolean).join(" / ") || "-"}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-300">Total Operators</p>
                  <p className="mt-1 font-black text-slate-900 dark:text-white">{selectedOperators.length} operator</p>
                </div>
                <div>
                  <p className="text-xs text-slate-400 dark:text-slate-300">Actual</p>
                  <p className="mt-1 font-black text-slate-900 dark:text-white">{actualText(selected)}</p>
                </div>
              </div>
            </div>

            <div className="rounded-md border border-slate-200 p-4 dark:border-slate-700">
              <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Assigned Operators</p>
              <div className="mt-3 space-y-2">
                {selectedOperators.length ? selectedOperators.map((operator) => (
                  <div className="rounded-md bg-slate-50 px-3 py-2 dark:bg-slate-800" key={operator.id}>
                    <div className="flex items-center justify-between gap-3">
                      <p className="truncate text-sm font-black text-slate-900 dark:text-white">{operator.full_name}</p>
                      <span className="shrink-0 rounded-md bg-white px-2 py-1 text-[11px] font-bold text-[#087ea4] dark:bg-slate-900 dark:text-cyan-300">{operator.shift}</span>
                    </div>
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-300">{operator.employee_no} / {operator.department}</p>
                    <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Scan {formatDateTime(operator.scanned_at)}</p>
                  </div>
                )) : (
                  <p className="rounded-md bg-slate-50 px-3 py-4 text-sm font-semibold text-slate-400 dark:bg-slate-800 dark:text-slate-200">No assigned operators.</p>
                )}
              </div>
            </div>
          </div>

            <div className="mt-5 grid gap-4 lg:grid-cols-[1fr_1fr_1.2fr]">
          <div className="rounded-md border border-slate-200 p-4 dark:border-slate-700">
            <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Active Operator</p>
            <div className="mt-3 space-y-2">
              {activeOperators.length ? activeOperators.map((operator) => (
                <div className="flex items-center justify-between gap-3 rounded-md bg-slate-50 px-3 py-2 dark:bg-slate-800" key={operator.id}>
                  <div className="min-w-0">
                    <p className="truncate text-sm font-black text-slate-900 dark:text-white">{operator.full_name}</p>
                    <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-300">{operator.employee_no} / {operator.shift_type || operator.operator_shift || "-"}</p>
                  </div>
                  <button
                    className="h-8 shrink-0 rounded-md border border-rose-200 px-2 text-xs font-bold text-rose-600 hover:bg-rose-50 disabled:opacity-40 dark:border-rose-500/30 dark:text-rose-300"
                    disabled={busy}
                    onClick={() => setOperatorPendingRemove(operator)}
                    type="button"
                  >
                    Remove
                  </button>
                </div>
              )) : (
                <p className="rounded-md bg-slate-50 px-3 py-4 text-sm font-semibold text-slate-400 dark:bg-slate-800 dark:text-slate-200">No active operators.</p>
              )}
            </div>
          </div>

          <div className="space-y-4">
          <div className="rounded-md border border-slate-200 p-4 dark:border-slate-700">
            <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Timestamp</p>
            <div className="mt-4 grid gap-3 text-sm">
              <div className="rounded-md bg-slate-50 px-3 py-2 dark:bg-slate-800">
                <p className="text-xs text-slate-400 dark:text-slate-300">Started</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatDateTime(selected?.started_at)}</p>
              </div>
              <div className="rounded-md bg-slate-50 px-3 py-2 dark:bg-slate-800">
                <p className="text-xs text-slate-400 dark:text-slate-300">Finished</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{formatDateTime(selected?.completed_at)}</p>
              </div>
            </div>
          </div>

          <div className="rounded-md border border-slate-200 p-4 dark:border-slate-700">
            <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Action</p>
            <div className="mt-4 grid max-w-sm grid-cols-2 gap-3">
              <button
                aria-disabled={!canStart || busy}
                className={`col-span-2 h-10 rounded-md border text-xs font-bold transition ${canStart ? "border-[#2563EB] bg-[#2563EB] text-white hover:bg-[#1D4ED8]" : "cursor-not-allowed border-slate-300 bg-slate-100 text-slate-400 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-500"} disabled:opacity-100`}
                disabled={busy}
                onClick={() => {
                  if (showBlockedAction(startBlockedReason())) return;
                  void action("start", { line_master_id: Number(selectedAreaMasterId) }, "Start timestamp saved.");
                }}
                type="button"
              >
                Start Order
              </button>
              <button
                aria-disabled={!canFinish || busy}
                className={`h-10 rounded-md border text-xs font-bold transition ${canFinish ? "border-[#16A34A] bg-[#16A34A] text-white hover:bg-[#15803D]" : "cursor-not-allowed border-slate-300 bg-slate-100 text-slate-400 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-500"} disabled:opacity-100`}
                disabled={busy}
                onClick={() => {
                  if (showBlockedAction(canFinish ? null : finishBlockedReason())) return;
                  void action("finish", undefined, "Finish timestamp saved.");
                }}
                type="button"
              >
                Finish
              </button>
              <button
                aria-disabled={!canCancelFinish || busy}
                className={`h-10 rounded-md border text-xs font-bold transition ${canCancelFinish ? "border-[#CBD5E1] bg-white text-[#475569] hover:bg-[#F8FAFC] dark:border-slate-600 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800" : "cursor-not-allowed border-slate-300 bg-slate-100 text-slate-400 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-500"} disabled:opacity-100`}
                disabled={busy}
                onClick={() => {
                  if (showBlockedAction(cancelFinishBlockedReason())) return;
                  setCancelFinishModalOpen(true);
                }}
                type="button"
              >
                Cancel Finish
              </button>
            </div>
          </div>
          </div>

          <div className="rounded-md border border-slate-200 p-4 dark:border-slate-700">
            <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Actual</p>
            <div className="mt-4 grid grid-cols-2 gap-3 text-sm">
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Actual</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{actualText(selected)}</p>
              </div>
              <div>
                <p className="text-xs text-slate-400 dark:text-slate-300">Reject</p>
                <p className="mt-1 font-black text-slate-900 dark:text-white">{rejectText(selected)}</p>
              </div>
            </div>
          </div>
            </div>
            </div>
          </section>
        </div>
      ) : null}
    </div>
  );
}
