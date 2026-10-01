"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useToast } from "@/context/ToastContext";
import { apiGet, apiPost, apiPut, getApiBaseUrl } from "@/lib/api";
import { clearStoredApiBaseUrl, getStoredApiBaseUrl, setStoredApiBaseUrl } from "@/lib/runtimeApiConfig";

type IntegrationSettingKey = "shiage_lot_no" | "internal_system_auth" | "internal_system_refresh";

type IntegrationSetting = {
  setting_key: string;
  base_url: string;
  endpoint_path: string;
  username?: string | null;
  password?: string | null;
  password_set?: boolean;
  filter_field_name: string;
  top: number;
  skip: number;
  is_active: boolean;
};

type EndpointPreviewRow = {
  lot_no?: string | null;
  order_no?: string | null;
  project_no?: string | null;
  weight?: number | null;
};

type InternalLoginTestResult = {
  url: string;
  status_code: number;
  success: boolean;
  message: string;
  token_preview?: string | null;
  refresh_token_preview?: string | null;
  refresh?: {
    url?: string | null;
    status_code: number;
    success: boolean;
    message: string;
    token_preview?: string | null;
    response_preview: string;
  } | null;
  response_preview: string;
};

type SettingDefinition = {
  key: IntegrationSettingKey;
  title: string;
  description: string;
  endpointPlaceholder: string;
  showCredentials?: boolean;
  showShiageOptions?: boolean;
};

const settingDefinitions: SettingDefinition[] = [
  {
    key: "shiage_lot_no",
    title: "Endpoint Shiage Lot No",
    description: "Dipakai saat scan Lot No untuk mengambil data dari Shiage.",
    endpointPlaceholder: "/fab-shiage-prod-res/",
    showShiageOptions: true,
  },
  {
    key: "internal_system_auth",
    title: "Endpoint Login External",
    description: "Dipakai untuk konfigurasi login ke endpoint external.",
    endpointPlaceholder: "/auth/login",
    showCredentials: true,
  },
  {
    key: "internal_system_refresh",
    title: "Endpoint Refresh External",
    description: "Dipakai untuk refresh token dari endpoint external setelah login berhasil.",
    endpointPlaceholder: "/auth/refresh",
  },
];

const defaultSettings: Record<IntegrationSettingKey, IntegrationSetting> = {
  shiage_lot_no: {
    setting_key: "shiage_lot_no",
    base_url: "",
    endpoint_path: "/fab-shiage-prod-res/",
    username: "",
    password: "",
    password_set: false,
    filter_field_name: "LOT_NO",
    top: 1,
    skip: 0,
    is_active: false,
  },
  internal_system_auth: {
    setting_key: "internal_system_auth",
    base_url: "",
    endpoint_path: "/auth/login",
    username: "",
    password: "",
    password_set: false,
    filter_field_name: "-",
    top: 1,
    skip: 0,
    is_active: true,
  },
  internal_system_refresh: {
    setting_key: "internal_system_refresh",
    base_url: "",
    endpoint_path: "/auth/refresh",
    username: "",
    password: "",
    password_set: false,
    filter_field_name: "-",
    top: 1,
    skip: 0,
    is_active: true,
  },
};

const inputClass =
  "mt-2 h-11 w-full rounded-lg border border-gray-300 bg-white px-4 text-sm font-semibold text-gray-900 shadow-theme-xs outline-none focus:border-brand-400 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90";

function normalizeBaseUrl(value: string) {
  return value.trim().replace(/\/+$/, "");
}

function normalizeEndpointPath(value: string, fallback: string) {
  const trimmed = value.trim();
  const next = trimmed || fallback;
  return next.startsWith("/") ? next : `/${next}`;
}

function buildUrl(setting: IntegrationSetting, fallbackEndpoint: string, lotNo = "") {
  const baseUrl = normalizeBaseUrl(setting.base_url || getApiBaseUrl());
  const endpointPath = normalizeEndpointPath(setting.endpoint_path, fallbackEndpoint);
  const url = new URL(`${baseUrl}${endpointPath}`);
  const normalizedLotNo = lotNo.trim();

  if (normalizedLotNo) {
    url.searchParams.set("lotNo", normalizedLotNo);
  }

  return url.toString();
}

async function readJsonOrText(response: Response) {
  const text = await response.text();
  if (!text) return null;

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function getPayloadMessage(payload: unknown) {
  if (payload && typeof payload === "object" && "message" in payload) {
    const message = (payload as { message?: unknown }).message;
    if (typeof message === "string") return message;
  }

  return typeof payload === "string" ? payload.slice(0, 300) : "";
}

export default function ProductionSettingPage() {
  const toast = useToast();
  const [apiBaseUrl, setApiBaseUrl] = useState("");
  const [settings, setSettings] = useState<Record<IntegrationSettingKey, IntegrationSetting>>(defaultSettings);
  const [lotNo, setLotNo] = useState("");
  const [loadingKey, setLoadingKey] = useState<IntegrationSettingKey | "test" | "login-test" | "">("");
  const [previewRows, setPreviewRows] = useState<EndpointPreviewRow[]>([]);
  const [loginTestResult, setLoginTestResult] = useState<InternalLoginTestResult | null>(null);

  useEffect(() => {
    setApiBaseUrl(getStoredApiBaseUrl() || getApiBaseUrl());
  }, []);

  useEffect(() => {
    let alive = true;

    async function loadSettings() {
      setLoadingKey("test");
      try {
        const rows = await Promise.all(
          settingDefinitions.map((definition) =>
            apiGet<IntegrationSetting>(`/api/production/settings/integration/${definition.key}`)
          )
        );

        if (!alive) return;

        setSettings((current) => ({
          ...current,
          shiage_lot_no: { ...current.shiage_lot_no, ...rows[0] },
          internal_system_auth: { ...current.internal_system_auth, ...rows[1] },
          internal_system_refresh: { ...current.internal_system_refresh, ...rows[2] },
        }));
      } catch (err) {
        if (alive) {
          toast.error({ message: err instanceof Error ? err.message : "Gagal memuat setting endpoint." });
        }
      } finally {
        if (alive) setLoadingKey("");
      }
    }

    void loadSettings();
    return () => {
      alive = false;
    };
  }, [toast]);

  function saveApiBaseUrl(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const saved = setStoredApiBaseUrl(apiBaseUrl);
    setApiBaseUrl(saved);
    toast.success({ message: "API server berhasil disimpan. Request berikutnya akan memakai URL ini." });
  }

  function resetApiBaseUrl() {
    clearStoredApiBaseUrl();
    const fallback = getApiBaseUrl();
    setApiBaseUrl(fallback);
    toast.success({ message: "API server dikembalikan ke default." });
  }

  const shiagePreviewUrl = useMemo(
    () => buildUrl(settings.shiage_lot_no, defaultSettings.shiage_lot_no.endpoint_path, lotNo),
    [lotNo, settings.shiage_lot_no]
  );

  function updateSetting<K extends keyof IntegrationSetting>(
    settingKey: IntegrationSettingKey,
    key: K,
    value: IntegrationSetting[K]
  ) {
    setSettings((current) => ({
      ...current,
      [settingKey]: {
        ...current[settingKey],
        [key]: value,
      },
    }));
  }

  async function saveSetting(event: FormEvent<HTMLFormElement>, definition: SettingDefinition) {
    event.preventDefault();
    const current = settings[definition.key];
    const nextSetting = {
      base_url: normalizeBaseUrl(current.base_url),
      endpoint_path: normalizeEndpointPath(current.endpoint_path, definition.endpointPlaceholder),
      username: current.username?.trim() || null,
      password: current.password?.trim() || null,
      filter_field_name: current.filter_field_name.trim() || defaultSettings[definition.key].filter_field_name,
      top: Math.max(1, Math.min(Number(current.top) || 1, 1000)),
      skip: Math.max(0, Number(current.skip) || 0),
      is_active: current.is_active,
    };

    setLoadingKey(definition.key);

    try {
      const saved = await apiPut<IntegrationSetting>(`/api/production/settings/integration/${definition.key}`, nextSetting);
      setSettings((currentSettings) => ({
        ...currentSettings,
        [definition.key]: { ...saved, password: "" },
      }));
      toast.success({ message: `${definition.title} berhasil disimpan.` });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal menyimpan setting endpoint." });
    } finally {
      setLoadingKey("");
    }
  }

  function buildSettingRequest(definition: SettingDefinition) {
    const current = settings[definition.key];
    return {
      base_url: normalizeBaseUrl(current.base_url),
      endpoint_path: normalizeEndpointPath(current.endpoint_path, definition.endpointPlaceholder),
      username: current.username?.trim() || null,
      password: current.password?.trim() || null,
      filter_field_name: current.filter_field_name.trim() || defaultSettings[definition.key].filter_field_name,
      top: Math.max(1, Math.min(Number(current.top) || 1, 1000)),
      skip: Math.max(0, Number(current.skip) || 0),
      is_active: current.is_active,
    };
  }

  async function testInternalLogin(definition: SettingDefinition) {
    setLoadingKey("login-test");
    setLoginTestResult(null);

    try {
      const result = await apiPost<InternalLoginTestResult>(
        "/api/production/settings/integration/internal-system-auth/test-login",
        buildSettingRequest(definition)
      );
      setLoginTestResult(result);
      if (result.success) {
        toast.success({ message: result.message || "Login external endpoint berhasil." });
      } else {
        toast.error({ message: result.message || "Login external endpoint gagal." });
      }
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal test login external endpoint." });
    } finally {
      setLoadingKey("");
    }
  }

  async function testShiageEndpoint() {
    setLoadingKey("test");
    setPreviewRows([]);

    try {
      const response = await fetch(shiagePreviewUrl, {
        headers: { Accept: "application/json" },
      });
      const payload = await readJsonOrText(response);

      if (
        !response.ok ||
        (payload && typeof payload === "object" && "success" in payload && payload.success === false)
      ) {
        throw new Error(getPayloadMessage(payload) || `Request failed with status ${response.status}`);
      }

      const rows =
        payload && typeof payload === "object" && "data" in payload && Array.isArray(payload.data)
          ? payload.data
          : Array.isArray(payload)
            ? payload
            : [];
      setPreviewRows(rows.slice(0, 10));
      toast.success({ message: `Endpoint berhasil diakses. ${rows.length} row diterima.` });
    } catch (err) {
      toast.error({ message: err instanceof Error ? err.message : "Gagal mengakses endpoint Shiage." });
    } finally {
      setLoadingKey("");
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.2em] text-[#0799c9]">Configuration</p>
        <h1 className="mt-2 text-2xl font-black text-slate-900 dark:text-white">Setting</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-300">
          Pusat konfigurasi endpoint produksi dan akses sistem.
        </p>
      </div>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
          <h2 className="font-bold text-slate-900 dark:text-white">API Server</h2>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-300">Alamat backend utama aplikasi.</p>
        </div>

        <form className="grid gap-4 p-5 lg:grid-cols-[1fr_auto]" onSubmit={saveApiBaseUrl}>
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Base URL</span>
            <input
              className={inputClass}
              onChange={(event) => setApiBaseUrl(event.target.value)}
              placeholder="http://192.168.1.10:5241"
              type="url"
              value={apiBaseUrl}
            />
          </label>

          <div className="flex items-end gap-3">
            <button
              className="h-11 rounded-lg bg-[#0799c9] px-5 text-sm font-bold text-white shadow-sm transition hover:bg-[#087ea4]"
              type="submit"
            >
              Save
            </button>
            <button
              className="h-11 rounded-lg border border-slate-200 px-5 text-sm font-bold text-slate-700 transition hover:border-[#0799c9] hover:text-[#0799c9] dark:border-slate-700 dark:text-slate-200"
              onClick={resetApiBaseUrl}
              type="button"
            >
              Reset
            </button>
          </div>
        </form>
      </section>

      {settingDefinitions.map((definition) => {
        const setting = settings[definition.key];
        const isSaving = loadingKey === definition.key;

        return (
          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900" key={definition.key}>
            <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
              <h2 className="font-bold text-slate-900 dark:text-white">{definition.title}</h2>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-300">{definition.description}</p>
            </div>

            <form className="grid gap-4 p-5 lg:grid-cols-[1.3fr_1fr_0.7fr]" onSubmit={(event) => void saveSetting(event, definition)}>
              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Base URL</span>
                <input
                  className={inputClass}
                  onChange={(event) => updateSetting(definition.key, "base_url", event.target.value)}
                  placeholder="http://localhost:5241"
                  type="url"
                  value={setting.base_url}
                />
              </label>

              <label className="block">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Endpoint Path</span>
                <input
                  className={inputClass}
                  onChange={(event) => updateSetting(definition.key, "endpoint_path", event.target.value)}
                  placeholder={definition.endpointPlaceholder}
                  value={setting.endpoint_path}
                />
              </label>

              <label className="flex items-end gap-3">
                <input
                  checked={setting.is_active}
                  className="mb-3 h-5 w-5 rounded border-slate-300 text-[#0799c9] focus:ring-[#0799c9]"
                  onChange={(event) => updateSetting(definition.key, "is_active", event.target.checked)}
                  type="checkbox"
                />
                <span className="mb-3 text-sm font-bold text-slate-700 dark:text-slate-200">Active</span>
              </label>

              {definition.showCredentials ? (
                <>
                  <label className="block">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Email / Username</span>
                    <input
                      autoComplete="off"
                      className={inputClass}
                      onChange={(event) => updateSetting(definition.key, "username", event.target.value)}
                      placeholder="Masukkan email atau username"
                      value={setting.username ?? ""}
                    />
                  </label>

                  <label className="block">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Password</span>
                    <input
                      autoComplete="new-password"
                      className={inputClass}
                      onChange={(event) => updateSetting(definition.key, "password", event.target.value)}
                      placeholder={setting.password_set ? "Sudah tersimpan. Isi untuk mengganti." : "Masukkan password"}
                      type="password"
                      value={setting.password ?? ""}
                    />
                  </label>
                </>
              ) : null}

              {definition.showShiageOptions ? (
                <>
                  <label className="block">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Filter Field</span>
                    <input
                      className={inputClass}
                      onChange={(event) => updateSetting(definition.key, "filter_field_name", event.target.value)}
                      placeholder="LOT_NO"
                      value={setting.filter_field_name}
                    />
                  </label>

                  <label className="block">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Top</span>
                    <input
                      className={inputClass}
                      min={1}
                      max={1000}
                      onChange={(event) => updateSetting(definition.key, "top", Number(event.target.value))}
                      type="number"
                      value={setting.top}
                    />
                  </label>

                  <label className="block">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Skip</span>
                    <input
                      className={inputClass}
                      min={0}
                      onChange={(event) => updateSetting(definition.key, "skip", Number(event.target.value))}
                      type="number"
                      value={setting.skip}
                    />
                  </label>
                </>
              ) : null}

              <div className="flex flex-wrap gap-3 lg:col-span-3">
                <button
                  className="h-11 rounded-lg bg-[#0799c9] px-5 text-sm font-bold text-white shadow-sm transition hover:bg-[#087ea4] disabled:cursor-not-allowed disabled:opacity-60"
                  disabled={isSaving}
                  type="submit"
                >
                  {isSaving ? "Saving..." : "Save Setting"}
                </button>
                {definition.showCredentials ? (
                  <button
                    className="h-11 rounded-lg border border-slate-200 px-5 text-sm font-bold text-slate-700 transition hover:border-[#0799c9] hover:text-[#0799c9] disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200"
                    disabled={loadingKey === "login-test"}
                    onClick={() => void testInternalLogin(definition)}
                    type="button"
                  >
                    {loadingKey === "login-test" ? "Testing..." : "Test Login"}
                  </button>
                ) : null}
              </div>

              {definition.showCredentials && loginTestResult ? (
                <div className="lg:col-span-3 rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-xs font-semibold text-slate-600 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-300">
                  <div className="grid gap-2 md:grid-cols-2">
                    <p>Status: <span className={loginTestResult.success ? "text-emerald-600" : "text-rose-600"}>{loginTestResult.status_code}</span></p>
                    <p>Token: {loginTestResult.token_preview || "-"}</p>
                    <p>Refresh Token: {loginTestResult.refresh_token_preview || "-"}</p>
                    <p>Refresh Status: {loginTestResult.refresh ? <span className={loginTestResult.refresh.success ? "text-emerald-600" : "text-rose-600"}>{loginTestResult.refresh.status_code}</span> : "-"}</p>
                    <p className="break-all md:col-span-2">URL: {loginTestResult.url}</p>
                    <p className="md:col-span-2">Message: {loginTestResult.message || "-"}</p>
                    <pre className="max-h-32 overflow-auto whitespace-pre-wrap rounded-md bg-white p-3 font-mono text-[11px] text-slate-700 dark:bg-slate-900 dark:text-slate-200 md:col-span-2">{loginTestResult.response_preview || "-"}</pre>
                    {loginTestResult.refresh ? (
                      <>
                        <p className="break-all md:col-span-2">Refresh URL: {loginTestResult.refresh.url || "-"}</p>
                        <p className="md:col-span-2">Refresh Message: {loginTestResult.refresh.message || "-"}</p>
                        <p className="md:col-span-2">Refresh Token Result: {loginTestResult.refresh.token_preview || "-"}</p>
                        <pre className="max-h-32 overflow-auto whitespace-pre-wrap rounded-md bg-white p-3 font-mono text-[11px] text-slate-700 dark:bg-slate-900 dark:text-slate-200 md:col-span-2">{loginTestResult.refresh.response_preview || "-"}</pre>
                      </>
                    ) : null}
                  </div>
                </div>
              ) : null}
            </form>
          </section>
        );
      })}

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
          <h2 className="font-bold text-slate-900 dark:text-white">Test Endpoint Shiage</h2>
        </div>

        <div className="grid gap-4 p-5">
          <label className="block">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-300">Test Lot No</span>
            <input
              className={inputClass}
              onChange={(event) => setLotNo(event.target.value)}
              placeholder="Kosongkan untuk ambil list, atau isi Lot No tertentu"
              value={lotNo}
            />
          </label>

          <div className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-3 text-xs font-semibold text-slate-600 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-300">
            <p className="break-all">{shiagePreviewUrl}</p>
          </div>

          <button
            className="h-11 w-fit rounded-lg border border-slate-200 px-5 text-sm font-bold text-slate-700 transition hover:border-[#0799c9] hover:text-[#0799c9] disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200"
            disabled={loadingKey === "test"}
            onClick={() => void testShiageEndpoint()}
            type="button"
          >
            {loadingKey === "test" ? "Testing..." : "Test Endpoint"}
          </button>
        </div>

        {previewRows.length > 0 ? (
          <div className="border-t border-slate-100 p-5 dark:border-slate-800">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[680px] border-separate border-spacing-0 text-left">
                <thead className="text-[11px] uppercase tracking-wider text-white">
                  <tr>
                    <th className="rounded-l-lg bg-[#0799c9] px-5 py-3">Lot No</th>
                    <th className="bg-[#0799c9] px-4 py-3">Order No</th>
                    <th className="bg-[#0799c9] px-4 py-3">Project No</th>
                    <th className="rounded-r-lg bg-[#0799c9] px-4 py-3 text-right">Weight</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {previewRows.map((row, index) => (
                    <tr className="text-sm" key={`${row.lot_no ?? "lot"}-${index}`}>
                      <td className="px-5 py-4 font-bold text-slate-900 dark:text-white">{row.lot_no || "-"}</td>
                      <td className="px-4 py-4 font-semibold text-slate-700 dark:text-slate-200">{row.order_no || "-"}</td>
                      <td className="px-4 py-4 font-semibold text-slate-700 dark:text-slate-200">{row.project_no || "-"}</td>
                      <td className="px-4 py-4 text-right text-xs font-semibold text-slate-500 dark:text-slate-300">{row.weight ?? "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}
      </section>
    </div>
  );
}
