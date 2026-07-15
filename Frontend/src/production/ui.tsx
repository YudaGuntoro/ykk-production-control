import type { ProductionWorkOrderStatus } from "./types";
import DatePicker from "@/components/form/date-picker";
import { useEffect, useMemo, useState } from "react";

export type ProductionDateFilterMode = "date" | "range";

export type ProductionDateRange = {
  startDate: string;
  endDate: string;
};

export const FILTER_DEBOUNCE_MS = 300;

export const statusStyles: Record<ProductionWorkOrderStatus, string> = {
  WAITING: "bg-amber-50 text-amber-700 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-500/20",
  IN_PROGRESS: "bg-blue-50 text-blue-700 ring-blue-200 dark:bg-blue-500/10 dark:text-blue-300 dark:ring-blue-500/20",
  FINISH: "bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/20",
};

export function StatusBadge({ status }: { status: ProductionWorkOrderStatus }) {
  return <span className={`inline-flex rounded-full px-2.5 py-1 text-[11px] font-bold ring-1 ring-inset ${statusStyles[status]}`}>{status.replaceAll("_", " ")}</span>;
}

export function useDebouncedValue<T>(value: T, delay = FILTER_DEBOUNCE_MS) {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedValue(value), delay);
    return () => clearTimeout(timeout);
  }, [value, delay]);

  return debouncedValue;
}

export function formatDateTime(value?: string | null) {
  if (!value) return "-";
  return new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}

export function todayParam() {
  const date = new Date();
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

function dateToParam(date: Date) {
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

function paramToDate(value: string) {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
}

export function dateValueToParam(value?: string | null) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return dateToParam(date);
}

export function isDateMatchFilter(
  value: string | null | undefined,
  mode: ProductionDateFilterMode,
  date: string,
  range: ProductionDateRange,
) {
  const current = dateValueToParam(value);
  if (!current) return false;

  if (mode === "date") {
    return !date || current === date;
  }

  if (range.startDate && current < range.startDate) {
    return false;
  }

  if (range.endDate && current > range.endDate) {
    return false;
  }

  return true;
}

type ProductionDatePickerProps = {
  className?: string;
  defaultValue?: string;
  label?: string;
  name?: string;
  onChange?: (value: string) => void;
  required?: boolean;
  value?: string;
};

export function ProductionDatePicker({
  className = "",
  defaultValue,
  label,
  name,
  onChange,
  required,
  value,
}: ProductionDatePickerProps) {
  const id = `production-date-${(name || label || "picker").toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;

  return (
    <div className={className}>
      <DatePicker
        className="h-11 w-full rounded-lg border-gray-300 bg-white px-4 py-2.5 pr-11 text-sm font-semibold text-gray-900 shadow-theme-xs focus:border-brand-400 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 dark:focus:border-brand-500"
        dateFormat="Y-m-d"
        defaultDate={value ?? defaultValue}
        id={id}
        iconClassName="size-5 text-gray-500 dark:text-gray-400"
        label={label}
        name={name}
        onChange={(_, dateStr) => onChange?.(dateStr)}
        placeholder="Select date"
        required={required}
      />
    </div>
  );
}

type ProductionDateFilterProps = {
  date: string;
  mode: ProductionDateFilterMode;
  onClear: () => void;
  onDateChange: (value: string) => void;
  onModeChange: (value: ProductionDateFilterMode) => void;
  onRangeChange: (value: ProductionDateRange) => void;
  range: ProductionDateRange;
  resetKey?: number;
};

export function ProductionDateFilter({
  date,
  mode,
  onClear,
  onDateChange,
  onModeChange,
  onRangeChange,
  range,
  resetKey = 0,
}: ProductionDateFilterProps) {
  const pickerDefaultDate = useMemo(
    () => range.startDate && range.endDate
      ? [paramToDate(range.startDate), paramToDate(range.endDate)]
      : date
        ? paramToDate(date)
        : undefined,
    [date, range.endDate, range.startDate],
  );

  return (
    <div className="flex flex-col gap-3 lg:flex-row lg:items-end">
      <div className="min-w-[260px]">
        <DatePicker
          key={`${mode}-${resetKey}`}
          className="h-11 rounded-lg border-gray-300 bg-white px-4 py-2.5 pr-11 text-sm font-semibold text-gray-900 shadow-theme-xs focus:border-brand-400 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 dark:focus:border-brand-500"
          dateFormat="d / m / Y"
          defaultDate={pickerDefaultDate}
          id="production-history-date-filter"
          mode="range"
          closeOnSelect={false}
          staticPosition={false}
          onChange={(selectedDates, _dateStr, instance) => {
            const startDate = selectedDates[0] ? dateToParam(selectedDates[0]) : "";
            const endDate = selectedDates[1] ? dateToParam(selectedDates[1]) : "";

            if (startDate && endDate) {
              onDateChange("");
              onModeChange("range");
              onRangeChange({ startDate, endDate });
              instance.close();
            }
          }}
          onClose={(selectedDates) => {
            const startDate = selectedDates[0] ? dateToParam(selectedDates[0]) : "";

            if (startDate && selectedDates.length === 1) {
              onDateChange(startDate);
              onModeChange("date");
              onRangeChange({ startDate: "", endDate: "" });
            }
          }}
          placeholder="Select Date"
        />
      </div>
      <button
        aria-label="Clear date filter"
        className="h-11 w-11 rounded-lg border border-slate-200 text-xs font-bold text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
        onClick={() => {
          onModeChange("date");
          onClear();
        }}
        type="button"
      >
        X
      </button>
    </div>
  );
}
