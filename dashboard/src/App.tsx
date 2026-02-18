import React, { useState } from "react";
import { Routes, Route } from "react-router-dom";
import Sidebar from "./components/layout/Sidebar";
import Header from "./components/layout/Header";
import DashboardPage from "./pages/DashboardPage";
import TradesPage from "./pages/TradesPage";
import PositionsPage from "./pages/PositionsPage";
import StrategiesPage from "./pages/StrategiesPage";
import RiskPage from "./pages/RiskPage";
import SettingsPage from "./pages/SettingsPage";
import { ToastProvider } from "./components/common/Toast";
import SetupWizard from "./components/common/SetupWizard";
import { useSignalR } from "./hooks/useSignalR";
import { usePortfolioOverview, useApiProviders } from "./api/hooks";

const App: React.FC = () => {
  const { isConnected, lastTick, lastTrade } = useSignalR();
  const { data: portfolio } = usePortfolioOverview();
  const { data: providers } = useApiProviders();

  const [setupDismissed, setSetupDismissed] = useState(() => {
    return localStorage.getItem("setup:dismissed") === "1";
  });

  const binanceConfigured = providers?.find((p) => p.name === "Binance")?.isConfigured;
  const showWizard = providers !== undefined && !binanceConfigured && !setupDismissed;

  const handleSetupComplete = () => {
    localStorage.setItem("setup:dismissed", "1");
    setSetupDismissed(true);
  };

  return (
    <ToastProvider>
      {showWizard && <SetupWizard onComplete={handleSetupComplete} />}

      <div className="flex min-h-screen bg-gray-900">
        <Sidebar isConnected={isConnected} />

        <div className="flex-1 ml-64 flex flex-col">
          <Header isConnected={isConnected} portfolio={portfolio} />

          <main className="flex-1 p-6 overflow-auto">
            <Routes>
              <Route
                path="/"
                element={
                  <DashboardPage
                    lastTick={lastTick}
                    lastTrade={lastTrade}
                  />
                }
              />
              <Route path="/trades" element={<TradesPage />} />
              <Route
                path="/positions"
                element={<PositionsPage lastTick={lastTick} />}
              />
              <Route path="/strategies" element={<StrategiesPage />} />
              <Route
                path="/risk"
                element={<RiskPage />}
              />
              <Route path="/settings" element={<SettingsPage />} />
            </Routes>
          </main>
        </div>
      </div>
    </ToastProvider>
  );
};

export default App;
