import type { Metadata } from "next";
import InternalSystemLogPage from "@/production/InternalSystemLogPage";

export const metadata: Metadata = { title: "Log | PT YKK AP Indonesia" };

export default function Page() {
  return <InternalSystemLogPage />;
}
