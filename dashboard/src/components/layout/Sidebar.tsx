import React from "react";
import { NavLink } from "react-router-dom";
import {
  LayoutDashboard,
  ArrowLeftRight,
  Briefcase,
  Brain,
  ShieldAlert,
  Settings,
  Activity,
  Terminal,
} from "lucide-react";
import type { ActivityEntry } from "@/api/types";

interface NavItem {
  to: string;
  label: string;
  icon: React.FC<{ className?: string }>;
}

const navItems: NavItem[] = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard },
  { to: "/trades", label: "Trades", icon: ArrowLeftRight },
  { to: "/positions", label: "Positions", icon: Briefcase },
  { to: "/strategies", label: "Strategies", icon: Brain },
  { to: "/risk", label: "Risk", icon: ShieldAlert },
  { to: "/settings", label: "Settings", icon: Settings },
  { to: "/system", label: "System", icon: Terminal },
];

interface SidebarProps {
  isConnected: boolean;
  lastActivity?: ActivityEntry | null;
}

const levelColors: Record<string, string> = {
  success: "text-emerald-400",
  info: "text-blue-400",
  warning: "text-amber-400",
  error: "text-red-400",
};

const Sidebar: React.FC<SidebarProps> = ({ isConnected, lastActivity }) => {
  return (
    <aside className="fixed left-0 top-0 h-screen w-64 bg-panel border-r border-panel-border flex flex-col z-30">
      {/* Logo */}
      <div className="px-6 py-5 border-b border-panel-border">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 bg-accent rounded-lg flex items-center justify-center">
            <Activity className="w-5 h-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-white tracking-tight">QuantTrader</h1>
            <p className="text-[10px] text-gray-500 uppercase tracking-widest">Trading Terminal</p>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === "/"}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                isActive
                  ? "bg-accent/10 text-accent-light"
                  : "text-gray-400 hover:text-white hover:bg-gray-800"
              }`
            }
          >
            <item.icon className="w-5 h-5 flex-shrink-0" />
            <span>{item.label}</span>
            {item.to === "/system" && lastActivity && (
              <span className="ml-auto w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
            )}
          </NavLink>
        ))}
      </nav>

      {/* Live activity ticker */}
      {lastActivity && (
        <div className="px-4 py-3 border-t border-panel-border bg-gray-900/50">
          <p className="text-[9px] text-gray-600 uppercase tracking-wider mb-1">Latest activity</p>
          <p className={`text-[10px] leading-tight line-clamp-2 ${levelColors[lastActivity.level] ?? "text-gray-400"}`}>
            <span className="font-semibold">[{lastActivity.service}]</span>{" "}
            {lastActivity.message}
          </p>
        </div>
      )}

      {/* System Status */}
      <div className="px-4 py-4 border-t border-panel-border">
        <div className="flex items-center gap-2">
          <span className="relative flex h-2.5 w-2.5">
            {isConnected && (
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-profit opacity-75" />
            )}
            <span
              className={`relative inline-flex rounded-full h-2.5 w-2.5 ${
                isConnected ? "bg-profit" : "bg-loss"
              }`}
            />
          </span>
          <span className={`text-xs font-medium ${isConnected ? "text-profit" : "text-loss"}`}>
            {isConnected ? "System Online" : "Disconnected"}
          </span>
        </div>
      </div>
    </aside>
  );
};

export default Sidebar;
