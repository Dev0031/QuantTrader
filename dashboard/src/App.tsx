import React from "react";
import { Routes, Route } from "react-router-dom";
import Sidebar from "./components/layout/Sidebar";
import Header from "./components/layout/Header";
import DashboardPage from "./pages/DashboardPage";
import TradesPage from "./pages/TradesPage";
import PositionsPage from "./pages/PositionsPage";
import StrategiesPage from "./pages/StrategiesPage";
import RiskPage from "./pages/RiskPage";
import SettingsPage from "./pages/SettingsPage";
import { useSignalR } from "./hooks/useSignalR";
import { usePortfolioOverview } from "./api/hooks";

const App: React.FC = () => {
  const { isConnected, lastTick, lastTrade } = useSignalR();
  const { data: portfolio } = usePortfolioOverview();

  return (
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
  );
};

export default App;
