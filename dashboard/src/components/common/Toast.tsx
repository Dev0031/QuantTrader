import React, { createContext, useCallback, useContext, useState } from "react";
import { X, CheckCircle, AlertCircle, AlertTriangle, Info } from "lucide-react";

type ToastType = "success" | "error" | "warning" | "info";

interface Toast {
  id: number;
  type: ToastType;
  message: string;
}

interface ToastContextValue {
  toast: (type: ToastType, message: string) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

let nextId = 0;

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within ToastProvider");
  return ctx;
}

export const ToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const removeToast = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const toast = useCallback(
    (type: ToastType, message: string) => {
      const id = ++nextId;
      setToasts((prev) => [...prev, { id, type, message }]);
      const delay = type === "error" || type === "warning" ? 10000 : 5000;
      setTimeout(() => removeToast(id), delay);
    },
    [removeToast]
  );

  const icon = (type: ToastType) => {
    switch (type) {
      case "success": return <CheckCircle className="w-4 h-4 text-green-400 flex-shrink-0" />;
      case "error": return <AlertCircle className="w-4 h-4 text-red-400 flex-shrink-0" />;
      case "warning": return <AlertTriangle className="w-4 h-4 text-yellow-400 flex-shrink-0" />;
      case "info": return <Info className="w-4 h-4 text-blue-400 flex-shrink-0" />;
    }
  };

  const borderColor = (type: ToastType) => {
    switch (type) {
      case "success": return "border-green-400/30";
      case "error": return "border-red-400/30";
      case "warning": return "border-yellow-400/30";
      case "info": return "border-blue-400/30";
    }
  };

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`flex items-start gap-2 p-3 rounded-lg bg-gray-800 border ${borderColor(t.type)} shadow-lg animate-in slide-in-from-right`}
          >
            {icon(t.type)}
            <span className="text-sm text-gray-200 flex-1">{t.message}</span>
            <button
              onClick={() => removeToast(t.id)}
              className="text-gray-500 hover:text-gray-300 flex-shrink-0"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
};
