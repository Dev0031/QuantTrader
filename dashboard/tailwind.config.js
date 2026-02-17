/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        profit: {
          DEFAULT: "#22c55e",
          light: "#4ade80",
          dark: "#16a34a",
          bg: "rgba(34, 197, 94, 0.1)",
        },
        loss: {
          DEFAULT: "#ef4444",
          light: "#f87171",
          dark: "#dc2626",
          bg: "rgba(239, 68, 68, 0.1)",
        },
        panel: {
          DEFAULT: "#111827",
          light: "#1f2937",
          border: "#374151",
        },
        accent: {
          DEFAULT: "#3b82f6",
          light: "#60a5fa",
          dark: "#2563eb",
        },
      },
      fontFamily: {
        mono: ['"JetBrains Mono"', '"Fira Code"', "monospace"],
      },
    },
  },
  plugins: [],
};
