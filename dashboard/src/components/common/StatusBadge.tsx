import React from "react";

type BadgeVariant = "success" | "danger" | "warning" | "info" | "neutral";

interface StatusBadgeProps {
  label: string;
  variant?: BadgeVariant;
  pulse?: boolean;
}

const variantStyles: Record<BadgeVariant, string> = {
  success: "bg-profit/20 text-profit border-profit/30",
  danger: "bg-loss/20 text-loss border-loss/30",
  warning: "bg-yellow-500/20 text-yellow-400 border-yellow-500/30",
  info: "bg-accent/20 text-accent-light border-accent/30",
  neutral: "bg-gray-700/50 text-gray-400 border-gray-600",
};

const StatusBadge: React.FC<StatusBadgeProps> = ({
  label,
  variant = "neutral",
  pulse = false,
}) => {
  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-medium rounded-full border ${variantStyles[variant]}`}
    >
      {pulse && (
        <span className="relative flex h-2 w-2">
          <span
            className={`animate-ping absolute inline-flex h-full w-full rounded-full opacity-75 ${
              variant === "success"
                ? "bg-profit"
                : variant === "danger"
                ? "bg-loss"
                : "bg-accent"
            }`}
          />
          <span
            className={`relative inline-flex rounded-full h-2 w-2 ${
              variant === "success"
                ? "bg-profit"
                : variant === "danger"
                ? "bg-loss"
                : "bg-accent"
            }`}
          />
        </span>
      )}
      {label}
    </span>
  );
};

export default StatusBadge;
