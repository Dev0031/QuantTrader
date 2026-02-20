import React, { useState, Suspense, lazy } from "react";
import { Routes, Route } from "react-router-dom";
import Sidebar from "./components/layout/Sidebar";
import Header from "./components/layout/Header";
import { ToastProvider } from "./components/common/Toast";
import SetupWizard from "./components/common/SetupWizard";
import { ErrorBoundary } from "./components/ui/ErrorBoundary";
import { ConnectionProvider, useConnection } from "./contexts/ConnectionContext";
import { usePortfolioOverview, useApiProviders } from "./api/hooks";

// Lazy-loaded pages — each loads only when navigated to
const DashboardPage = lazy(() => import("./pages/DashboardPage"));
const TradesPage = lazy(() => import("./pages/TradesPage"));
const PositionsPage = lazy(() => import("./pages/PositionsPage"));
const StrategiesPage = lazy(() => import("./pages/StrategiesPage"));
const RiskPage = lazy(() => import("./pages/RiskPage"));
const SettingsPage = lazy(() => import("./pages/SettingsPage"));
const SystemPage = lazy(() => import("./pages/SystemPage"));

function PageSpinner() {
  return (
    <div className="flex items-center justify-center h-64">
      <div className="w-8 h-8 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
    </div>
  );
}

function AppInner() {
  const { isConnected, lastTick, lastTrade, lastActivity } = useConnection();
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
        <Sidebar isConnected={isConnected} lastActivity={lastActivity} />

        <div className="flex-1 ml-64 flex flex-col">
          <Header isConnected={isConnected} portfolio={portfolio} />

          <main className="flex-1 p-6 overflow-auto">
            <Routes>
              <Route
                path="/"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <DashboardPage lastTick={lastTick} lastTrade={lastTrade} />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
              <Route
                path="/trades"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <TradesPage />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
              <Route
                path="/positions"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <PositionsPage lastTick={lastTick} />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
              <Route
                path="/strategies"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <StrategiesPage />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
              <Route
                path="/risk"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <RiskPage />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
              <Route
                path="/settings"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <SettingsPage />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
              <Route
                path="/system"
                element={
                  <ErrorBoundary>
                    <Suspense fallback={<PageSpinner />}>
                      <SystemPage />
                    </Suspense>
                  </ErrorBoundary>
                }
              />
            </Routes>
          </main>
        </div>
      </div>
    </ToastProvider>
  );
}

const App: React.FC = () => {
  return (
    <ConnectionProvider>
      <AppInner />
    </ConnectionProvider>
  );
};

export default App;
