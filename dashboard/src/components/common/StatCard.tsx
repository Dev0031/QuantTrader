import React from "react";
import type { LucideIcon } from "lucide-react";

interface StatCardProps {
  title: string;
  value: string;
  change?: number;
  changeLabel?: string;
  icon: LucideIcon;
  iconColor?: string;
  valueColor?: string;
}

const StatCard: React.FC<StatCardProps> = ({
  title,
  value,
  change,
  changeLabel,
  icon: Icon,
  iconColor = "text-accent",
  valueColor = "text-white",
}) => {
  const changeColor =
    change !== undefined
      ? change >= 0
        ? "text-profit"
        : "text-loss"
      : "";
  const changeSign = change !== undefined && change >= 0 ? "+" : "";

  return (
    <div className="card">
      <div className="flex items-center justify-between mb-3">
        <span className="card-header mb-0">{title}</span>
        <div className={`p-2 rounded-lg bg-gray-800 ${iconColor}`}>
          <Icon className="w-5 h-5" />
        </div>
      </div>
      <div className={`text-2xl font-bold font-mono ${valueColor}`}>{value}</div>
      {change !== undefined && (
        <div className={`mt-1 text-sm font-medium ${changeColor}`}>
          {changeSign}
          {change.toFixed(2)}%{changeLabel ? ` ${changeLabel}` : ""}
        </div>
      )}
    </div>
  );
};

export default StatCard;
