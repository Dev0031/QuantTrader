import React, { useState, useEffect } from "react";
import { Info, ChevronDown, ChevronRight } from "lucide-react";

interface PageGuideProps {
  pageId: string;
  title?: string;
  children: React.ReactNode;
}

const PageGuide: React.FC<PageGuideProps> = ({ pageId, title = "How This Works", children }) => {
  const storageKey = `guide:${pageId}`;
  const [expanded, setExpanded] = useState(() => {
    const stored = localStorage.getItem(storageKey);
    return stored === "true";
  });

  useEffect(() => {
    localStorage.setItem(storageKey, String(expanded));
  }, [expanded, storageKey]);

  return (
    <div className="border border-dashed border-gray-700 rounded-lg bg-gray-800/30">
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center gap-2 px-4 py-2.5 text-left hover:bg-gray-800/50 transition-colors rounded-lg"
      >
        <Info className="w-4 h-4 text-gray-500 flex-shrink-0" />
        <span className="text-sm text-gray-400 font-medium">{title}</span>
        {expanded ? (
          <ChevronDown className="w-4 h-4 text-gray-500 ml-auto" />
        ) : (
          <ChevronRight className="w-4 h-4 text-gray-500 ml-auto" />
        )}
      </button>
      {expanded && (
        <div className="px-4 pb-4 text-xs text-gray-400 space-y-2 leading-relaxed">
          {children}
        </div>
      )}
    </div>
  );
};

export default PageGuide;
