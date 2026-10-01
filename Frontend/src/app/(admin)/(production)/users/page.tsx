import type { Metadata } from "next";
import UserManagementPage from "@/production/UserManagementPage";

export const metadata: Metadata = { title: "Login User | PT YKK AP Indonesia" };

export default function Page() {
  return <UserManagementPage />;
}
