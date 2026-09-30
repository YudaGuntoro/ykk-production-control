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
  pro?: boolean;
  new?: boolean;
};

export type NavItem = {
  name: string;
  icon: React.ReactNode;
  path?: string;
  subItems?: NavSubItem[];
};

export const navItems: NavItem[] = [
  {
    icon: <GridIcon />,
    name: "Dashboard",
    path: "/",
  },
  {
    icon: <BoltIcon />,
    name: "Production Control",
    path: "/production-control",
  },
  {
    icon: <BoxCubeIcon />,
    name: "Master Data",
    subItems: [
      { name: "Shift", path: "/shift-master" },
      { name: "Line", path: "/line-master" },
      { name: "Operator List", path: "/pic-cards" },
    ],
  },
  {
    icon: <ListIcon />,
    name: "Log",
    path: "/log",
  },
  {
    icon: <DocsIcon />,
    name: "Production Activity",
    subItems: [
      { name: "Production Activity", path: "/production-history" },
      { name: "Production History", path: "/cutting-lists" },
    ],
  },
  {
    icon: <PlugInIcon />,
    name: "Setting",
    path: "/production-setting",
  },
];
