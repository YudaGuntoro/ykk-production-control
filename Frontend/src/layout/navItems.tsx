import React from "react";
import {
  BoxCubeIcon,
  BoltIcon,
  DocsIcon,
  GridIcon,
  ListIcon,
  PlugInIcon,
} from "../icons/index";

export type NavSubItem = {
  name: string;
  path: string;
  pageKey: string;
  pro?: boolean;
  new?: boolean;
};

export type NavItem = {
  name: string;
  icon: React.ReactNode;
  pageKey?: string;
  path?: string;
  subItems?: NavSubItem[];
};

export const navItems: NavItem[] = [
  {
    icon: <GridIcon />,
    name: "Dashboard",
    pageKey: "dashboard",
    path: "/",
  },
  {
    icon: <BoltIcon />,
    name: "Production Control",
    pageKey: "production_control",
    path: "/production-control",
  },
  {
    icon: <BoxCubeIcon />,
    name: "Master Data",
    subItems: [
      { name: "Shift", pageKey: "shift_master", path: "/shift-master" },
      { name: "Line", pageKey: "line_master", path: "/line-master" },
      { name: "Operator List", pageKey: "operator_list", path: "/pic-cards" },
      { name: "Users", pageKey: "users", path: "/users" },
      { name: "Role Access", pageKey: "role_access", path: "/role-access" },
    ],
  },
  {
    icon: <ListIcon />,
    name: "Activity Log",
    pageKey: "activity_log",
    path: "/log",
  },
  {
    icon: <DocsIcon />,
    name: "Production Activity",
    subItems: [
      { name: "Production Activity", pageKey: "production_activity", path: "/production-history" },
      { name: "Production History", pageKey: "production_history", path: "/cutting-lists" },
    ],
  },
  {
    icon: <PlugInIcon />,
    name: "Setting",
    pageKey: "setting",
    path: "/production-setting",
  },
];
