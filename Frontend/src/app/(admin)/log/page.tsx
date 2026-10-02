import type { Metadata } from "next";
import InternalSystemLogPage from "@/production/InternalSystemLogPage";

export const metadata: Metadata = { title: "Activity Log | PT YKK AP Indonesia" };

export default function Page() {
  return <InternalSystemLogPage />;
}
