import React, { useState } from "react";
import { AlertCircle, AlertTriangle, Info, X } from "lucide-react";
import { useNavigate } from "react-router-dom";

interface DataStatusBannerProps {
  type: "error" | "warning" | "info";
  title: string;
  message: string;
  action?: { label: string; href: string };
  dismissKey?: string;
}

const DataStatusBanner: React.FC<DataStatusBannerProps> = ({
  type,
  title,
  message,
  action,
  dismissKey,
}) => {
  const navigate = useNavigate();
  const [dismissed, setDismissed] = useState(() => {
    if (!dismissKey) return false;
    return sessionStorage.getItem(`banner:${dismissKey}`) === "1";
  });

  if (dismissed) return null;

  const handleDismiss = () => {
    setDismissed(true);
    if (dismissKey) sessionStorage.setItem(`banner:${dismissKey}`, "1");
  };

  const styles = {
    error: "bg-red-400/5 border-red-400/20 text-red-400",
    warning: "bg-yellow-400/5 border-yellow-400/20 text-yellow-400",
    info: "bg-blue-400/5 border-blue-400/20 text-blue-400",
  };

  const icons = {
    error: <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />,
    warning: <AlertTriangle className="w-4 h-4 flex-shrink-0 mt-0.5" />,
    info: <Info className="w-4 h-4 flex-shrink-0 mt-0.5" />,
  };

  return (
    <div className={`p-3 rounded-lg border flex items-start gap-3 ${styles[type]}`}>
      {icons[type]}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs mt-0.5 opacity-80">{message}</p>
        {action && (
          <button
            onClick={() => navigate(action.href)}
            className="mt-2 text-xs font-medium underline hover:no-underline"
          >
            {action.label}
          </button>
        )}
      </div>
      <button onClick={handleDismiss} className="opacity-50 hover:opacity-100 flex-shrink-0">
        <X className="w-3.5 h-3.5" />
      </button>
    </div>
  );
};

export default DataStatusBanner;
