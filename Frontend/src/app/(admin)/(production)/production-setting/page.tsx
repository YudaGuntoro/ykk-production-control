import type { Metadata } from "next";
import ProductionSettingPage from "@/production/ProductionSettingPage";

export const metadata: Metadata = {
  title: "Setting | PT YKK AP Indonesia",
};

export default function Page() {
  return <ProductionSettingPage />;
}
