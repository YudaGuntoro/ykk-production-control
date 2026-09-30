"use client";

import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, apiPost } from "@/lib/api";
import type { PicCard } from "./types";
import { formatDateTime } from "./ui";
import QRCode from "qrcode";
import { toPng } from "html-to-image";
import { jsPDF } from "jspdf";

const inputClass =
  "h-11 w-full rounded-md border border-slate-200 bg-white px-3 text-sm text-slate-800 placeholder:text-slate-500 outline-none focus:border-[#0799c9] focus:ring-2 focus:ring-cyan-500/10 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-50 dark:placeholder:text-slate-300";

export default function PicCardsPage() {
  const toast = useToast();
  const [items, setItems] = useState<PicCard[]>([]);
  const [cardUid, setCardUid] = useState("");
  const [employeeNo, setEmployeeNo] = useState("");
  const [fullName, setFullName] = useState("");
  const [pendingDeactivate, setPendingDeactivate] = useState<PicCard | null>(null);
  const [qrOperator, setQrOperator] = useState<PicCard | null>(null);
  const [qrImageUrl, setQrImageUrl] = useState("");
  const [qrSaving, setQrSaving] = useState(false);
  const [pdfSaving, setPdfSaving] = useState(false);
  const [busy, setBusy] = useState(false);
  const qrCardRef = useRef<HTMLDivElement | null>(null);

  const load = useCallback(async () => {
    try {
      setItems(await apiGet<PicCard[]>("/api/production/pic-cards"));
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to load data." });
    }
  }, [toast]);

  useEffect(() => {
    void load();
  }, [load]);

  async function registerOperator(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!cardUid.trim() || !employeeNo.trim() || !fullName.trim()) {
      toast.error({ message: "Scan ID, NIK, dan Nama wajib diisi." });
      return;
    }

    setBusy(true);
    try {
      await apiPost<PicCard>("/api/production/pic-cards", {
        card_uid: cardUid.trim(),
        employee_no: employeeNo.trim(),
        full_name: fullName.trim(),
      });
      setCardUid("");
      setEmployeeNo("");
      setFullName("");
      toast.success({ message: "Operator berhasil didaftarkan." });
      await load();
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to register operator." });
    } finally {
      setBusy(false);
    }
  }

  async function deactivateOperator() {
    if (!pendingDeactivate) {
      return;
    }

    setBusy(true);
    try {
      await apiPost<PicCard>(`/api/production/pic-cards/${pendingDeactivate.id}/deactivate`);
      toast.success({ message: "Operator berhasil dinonaktifkan." });
      setPendingDeactivate(null);
      await load();
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to deactivate operator." });
    } finally {
      setBusy(false);
    }
  }

  async function openQr(operator: PicCard) {
    setQrOperator(operator);
    setQrImageUrl("");

    try {
      const dataUrl = await QRCode.toDataURL(operator.employee_no, {
        errorCorrectionLevel: "M",
        margin: 2,
        scale: 8,
        color: {
          dark: "#0f172a",
          light: "#ffffff",
        },
      });
      setQrImageUrl(dataUrl);
    } catch (err) {
      setQrOperator(null);
      toast.error({ message: err instanceof Error ? err.message : "Failed to generate QR." });
    }
  }

  function qrFileName(operator: PicCard, extension: "png" | "pdf") {
    const safeName = `${operator.employee_no}-${operator.full_name}`.replace(/[^a-z0-9-_]+/gi, "-").replace(/-+/g, "-");
    return `QR-Operator-${safeName}.${extension}`;
  }

  async function createQrCardImage() {
    if (!qrCardRef.current) {
      throw new Error("QR card is not ready.");
    }

    return toPng(qrCardRef.current, {
      cacheBust: true,
      pixelRatio: 2,
      backgroundColor: "#ffffff",
    });
  }

  async function saveQrAsImage() {
    if (!qrOperator) return;

    setQrSaving(true);
    try {
      const dataUrl = await createQrCardImage();
      const link = document.createElement("a");
      link.href = dataUrl;
      link.download = qrFileName(qrOperator, "png");
      document.body.appendChild(link);
      link.click();
      link.remove();
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to save QR image." });
    } finally {
      setQrSaving(false);
    }
  }

  async function createQrDataUrl(value: string) {
    return QRCode.toDataURL(value, {
      errorCorrectionLevel: "M",
      margin: 1,
      scale: 6,
      color: {
        dark: "#111827",
        light: "#ffffff",
      },
    });
  }

  function drawOperatorQrCard(pdf: jsPDF, operator: PicCard, qrDataUrl: string, x: number, y: number, width: number) {
    const qrSize = 28;
    const qrX = x + (width - qrSize) / 2;
    pdf.setDrawColor(203, 213, 225);
    pdf.setFillColor(255, 255, 255);
    pdf.roundedRect(x, y, width, 57, 2, 2, "FD");

    pdf.setDrawColor(226, 232, 240);
    pdf.setFillColor(248, 250, 252);
    pdf.roundedRect(qrX - 2, y + 4, qrSize + 4, qrSize + 4, 1.5, 1.5, "FD");
    pdf.addImage(qrDataUrl, "PNG", qrX, y + 6, qrSize, qrSize);

    pdf.setFont("helvetica", "bold");
    pdf.setFontSize(6);
    pdf.setTextColor(7, 153, 201);
    pdf.text("NIK", x + width / 2, y + 40, { align: "center" });

    pdf.setFontSize(8);
    pdf.setTextColor(17, 24, 39);
    pdf.text(operator.employee_no || "-", x + width / 2, y + 44, { align: "center", maxWidth: width - 5 });

    pdf.setFont("helvetica", "normal");
    pdf.setFontSize(6);
    pdf.setTextColor(75, 85, 99);
    pdf.text(operator.full_name || "-", x + width / 2, y + 48.5, { align: "center", maxWidth: width - 5 });

    pdf.setFontSize(5);
    pdf.setTextColor(100, 116, 139);
    pdf.text(`${operator.department || "-"} / ${operator.shift || "-"}`, x + width / 2, y + 52.5, { align: "center", maxWidth: width - 5 });
  }

  async function saveOperatorsPdf(selectedOperators: PicCard[], fileName: string) {
    if (!selectedOperators.length) return;

    setPdfSaving(true);
    try {
      const pdf = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });
      const pageWidth = pdf.internal.pageSize.getWidth();
      const pageHeight = pdf.internal.pageSize.getHeight();
      const marginX = 10;
      const marginTop = 14;
      const gapX = 4;
      const gapY = 6;
      const columns = 5;
      const cardWidth = (pageWidth - marginX * 2 - gapX * (columns - 1)) / columns;
      const cardHeight = 57;
      let x = marginX;
      let y = marginTop;

      pdf.setFont("helvetica", "bold");
      pdf.setFontSize(12);
      pdf.setTextColor(17, 24, 39);
      pdf.text("Operator QR List", marginX, 8);
      pdf.setFont("helvetica", "normal");
      pdf.setFontSize(8);
      pdf.setTextColor(100, 116, 139);
      pdf.text("QR berisi NIK operator. Layout 5 QR per baris.", marginX + 44, 8);

      for (let index = 0; index < selectedOperators.length; index++) {
        if (index > 0 && index % columns === 0) {
          x = marginX;
          y += cardHeight + gapY;
        }

        if (y + cardHeight > pageHeight - 10) {
          pdf.addPage();
          pdf.setFont("helvetica", "bold");
          pdf.setFontSize(12);
          pdf.setTextColor(17, 24, 39);
          pdf.text("Operator QR List", marginX, 8);
          pdf.setFont("helvetica", "normal");
          pdf.setFontSize(8);
          pdf.setTextColor(100, 116, 139);
          pdf.text("QR berisi NIK operator. Layout 5 QR per baris.", marginX + 44, 8);
          x = marginX;
          y = marginTop;
        }

        const operator = selectedOperators[index];
        const qrDataUrl = await createQrDataUrl(operator.employee_no);
        drawOperatorQrCard(pdf, operator, qrDataUrl, x, y, cardWidth);
        x += cardWidth + gapX;
      }

      pdf.save(fileName);
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Failed to save QR PDF." });
    } finally {
      setPdfSaving(false);
    }
  }

  async function saveSingleOperatorPdf(operator: PicCard) {
    await saveOperatorsPdf([operator], qrFileName(operator, "pdf"));
  }

  async function saveOperatorListPdf() {
    await saveOperatorsPdf(items, `QR-Operator-List-${new Date().toISOString().slice(0, 10)}.pdf`);
  }

  return (
    <div className="space-y-6">
      {qrOperator ? (
        <div className="fixed inset-0 z-[100010] flex items-center justify-center bg-slate-950/55 px-4">
          <div className="relative w-full max-w-sm overflow-hidden rounded-lg bg-white shadow-xl dark:bg-slate-900">
            <button
              aria-label="Close QR"
              className="absolute right-4 top-4 flex size-8 items-center justify-center rounded-md text-xs font-black text-slate-400 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white"
              onClick={() => {
                setQrOperator(null);
                setQrImageUrl("");
                setQrSaving(false);
              }}
              type="button"
            >
              X
            </button>

            <div ref={qrCardRef} className="bg-white px-6 pb-5 pt-5 text-center text-slate-950">
              <div className="pr-10 text-left">
                <div>
                  <h2 className="text-lg font-black text-slate-950">QR Operator</h2>
                  <p className="mt-1 text-sm font-bold text-slate-500">{qrOperator.full_name}</p>
                </div>
              </div>

              <div className="mt-5 rounded-lg border border-slate-200 bg-white p-4">
                {qrImageUrl ? (
                  <img alt={`QR NIK ${qrOperator.employee_no}`} className="mx-auto size-56" src={qrImageUrl} />
                ) : (
                  <div className="mx-auto flex size-56 items-center justify-center text-sm font-bold text-slate-400">Generating QR...</div>
                )}
              </div>

              <p className="mt-5 text-[11px] font-black uppercase tracking-wider text-slate-500">NIK</p>
              <p className="mt-1 break-all text-2xl font-black text-slate-950">{qrOperator.employee_no}</p>
              <p className="mt-3 text-xs font-semibold text-slate-500">QR berisi NIK operator dan bisa discan.</p>
            </div>

            <div className="grid gap-3 border-t border-slate-100 bg-slate-50 p-4 dark:border-slate-800 dark:bg-slate-950 sm:grid-cols-2">
              <button
                className="h-10 w-full rounded-lg bg-[#0799c9] px-4 text-xs font-black text-white shadow-sm hover:bg-[#087ea4] disabled:cursor-not-allowed disabled:opacity-60"
                disabled={!qrImageUrl || qrSaving || pdfSaving}
                onClick={() => void saveQrAsImage()}
                type="button"
              >
                {qrSaving ? "Saving..." : "Save Image"}
              </button>
              <button
                className="h-10 w-full rounded-lg bg-red-600 px-4 text-xs font-black text-white shadow-sm hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
                disabled={!qrImageUrl || qrSaving || pdfSaving}
                onClick={() => void saveSingleOperatorPdf(qrOperator)}
                type="button"
              >
                {pdfSaving ? "Saving..." : "Print PDF"}
              </button>
            </div>
          </div>
        </div>
      ) : null}

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
        <div className="flex flex-col gap-3 border-b border-slate-100 px-5 py-4 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="font-bold text-slate-900 dark:text-white">List Operator</h2>
            <p className="mt-1 text-xs text-slate-400 dark:text-slate-300">Operator master data.</p>
          </div>
          <button
            className="h-10 rounded-lg bg-red-600 px-4 text-xs font-black text-white shadow-sm hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
            disabled={!items.length || pdfSaving}
            onClick={() => void saveOperatorListPdf()}
            type="button"
          >
            {pdfSaving ? "Preparing PDF..." : "Print List PDF"}
          </button>
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
                <th className="rounded-r-lg bg-[#0799c9] px-5 py-3 text-center">Action</th>
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
                    <div className="flex justify-end gap-2">
                      <button
                        className="h-9 rounded-md border border-cyan-200 px-3 text-xs font-bold text-[#0799c9] hover:bg-cyan-50 dark:border-cyan-500/30 dark:text-cyan-300 dark:hover:bg-cyan-500/10"
                        onClick={() => void openQr(item)}
                        type="button"
                      >
                        QR
                      </button>
                      <button
                        className="h-9 rounded-md border border-rose-200 px-3 text-xs font-bold text-rose-600 hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-rose-500/30 dark:text-rose-300"
                        disabled={!item.is_active || busy}
                        onClick={() => setPendingDeactivate(item)}
                        type="button"
                      >
                        Nonaktifkan
                      </button>
                    </div>
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
