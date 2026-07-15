import { useEffect, useRef } from 'react';
import flatpickr from 'flatpickr';
import 'flatpickr/dist/flatpickr.css';
import { twMerge } from 'tailwind-merge';
import Label from './Label';
import { CalenderIcon } from '../../icons';
import Hook = flatpickr.Options.Hook;
import DateOption = flatpickr.Options.DateOption;

type PropsType = {
  id: string;
  mode?: "single" | "multiple" | "range" | "time";
  onChange?: Hook | Hook[];
  defaultDate?: DateOption | DateOption[];
  label?: string;
  placeholder?: string;
  enableTime?: boolean;
  dateFormat?: string;
  className?: string;
  iconClassName?: string;
  name?: string;
  required?: boolean;
  altInput?: boolean;
  altFormat?: string;
  closeOnSelect?: boolean;
  onClose?: Hook | Hook[];
  staticPosition?: boolean;
};

export default function DatePicker({
  id,
  mode,
  onChange,
  label,
  defaultDate,
  placeholder,
  enableTime = false,
  dateFormat,
  className,
  iconClassName,
  name,
  required,
  altInput,
  altFormat,
  closeOnSelect,
  onClose,
  staticPosition,
}: PropsType) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const onChangeRef = useRef(onChange);
  const onCloseRef = useRef(onClose);
  const resolvedDateFormat = dateFormat || (enableTime ? "Y-m-d H:i" : "Y-m-d");

  onChangeRef.current = onChange;
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!inputRef.current) {
      return;
    }

    const runHook = (hook: Hook | Hook[] | undefined, ...args: Parameters<Hook>) => {
      if (Array.isArray(hook)) {
        hook.forEach((item) => item(...args));
        return;
      }

      hook?.(...args);
    };

    const flatPickr = flatpickr(inputRef.current, {
      mode: mode || "single",
      static: staticPosition ?? true,
      monthSelectorType: "static",
      dateFormat: resolvedDateFormat,
      defaultDate,
      onChange: (...args) => runHook(onChangeRef.current, ...args),
      onClose: (...args) => runHook(onCloseRef.current, ...args),
      enableTime,
      time_24hr: true,
      allowInput: true,
      disableMobile: true,
      altInput,
      altFormat,
      closeOnSelect: closeOnSelect ?? mode !== "range",
    });

    return () => {
      flatPickr.destroy();
    };
  }, [mode, id, defaultDate, resolvedDateFormat, enableTime, altInput, altFormat, closeOnSelect, staticPosition]);

  return (
    <div>
      {label && <Label htmlFor={id}>{label}</Label>}

      <div className="relative">
        <input
          id={id}
          name={name}
          ref={inputRef}
          placeholder={placeholder}
          required={required}
          className={twMerge(
            "h-11 w-full appearance-none rounded-lg border border-gray-300 bg-white px-4 py-2.5 pr-11 text-sm font-medium text-gray-800 shadow-theme-xs outline-hidden transition placeholder:text-gray-400 focus:border-brand-400 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 dark:placeholder:text-white/30 dark:focus:border-brand-500",
            className
          )}
        />

        <span className="pointer-events-none absolute right-3.5 top-1/2 flex size-5 -translate-y-1/2 items-center justify-center text-gray-500 leading-none dark:text-gray-400">
          <CalenderIcon
            className={twMerge("block size-[18px] overflow-visible", iconClassName)}
          />
        </span>
      </div>
    </div>
  );
}
