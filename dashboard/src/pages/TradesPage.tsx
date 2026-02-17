import React, { useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Filter,
  Trophy,
  Target,
  BarChart3,
} from "lucide-react";
import TradeTable from "@/components/common/TradeTable";
import StatCard from "@/components/common/StatCard";
import { useTrades, useTradeStats } from "@/api/hooks";
import type { TradeFilter } from "@/api/types";

const TradesPage: React.FC = () => {
  const [filters, setFilters] = useState<TradeFilter>({
    page: 1,
    pageSize: 20,
  });
  const [showFilters, setShowFilters] = useState(false);
  const [symbolInput, setSymbolInput] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [sideFilter, setSideFilter] = useState<"" | "Buy" | "Sell">("");

  const { data: tradesData, isLoading } = useTrades(filters);
  const { data: stats } = useTradeStats({
    startDate: filters.startDate,
    endDate: filters.endDate,
  });

  const applyFilters = () => {
    setFilters({
      ...filters,
      page: 1,
      symbol: symbolInput || undefined,
      side: sideFilter || undefined,
      startDate: startDate || undefined,
      endDate: endDate || undefined,
    });
  };

  const clearFilters = () => {
    setSymbolInput("");
    setStartDate("");
    setEndDate("");
    setSideFilter("");
    setFilters({ page: 1, pageSize: 20 });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Trade History</h1>
        <button
          onClick={() => setShowFilters(!showFilters)}
          className="flex items-center gap-2 btn-primary text-sm"
        >
          <Filter className="w-4 h-4" />
          Filters
        </button>
      </div>

      {/* Performance Stats */}
      {stats && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            title="Total Trades"
            value={stats.totalTrades.toString()}
            icon={BarChart3}
            iconColor="text-accent"
          />
          <StatCard
            title="Win Rate"
            value={`${stats.winRate.toFixed(1)}%`}
            icon={Trophy}
            iconColor="text-yellow-400"
            valueColor={stats.winRate >= 50 ? "text-profit" : "text-loss"}
          />
          <StatCard
            title="Profit Factor"
            value={stats.profitFactor.toFixed(2)}
            icon={Target}
            iconColor="text-purple-400"
            valueColor={stats.profitFactor >= 1 ? "text-profit" : "text-loss"}
          />
          <StatCard
            title="Avg R:R"
            value={stats.averageRR.toFixed(2)}
            icon={BarChart3}
            iconColor="text-cyan-400"
          />
        </div>
      )}

      {/* Filters */}
      {showFilters && (
        <div className="card">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label className="block text-xs text-gray-400 mb-1">Symbol</label>
              <input
                type="text"
                value={symbolInput}
                onChange={(e) => setSymbolInput(e.target.value.toUpperCase())}
                placeholder="e.g. BTCUSDT"
                className="input-field w-full"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-400 mb-1">Side</label>
              <select
                value={sideFilter}
                onChange={(e) => setSideFilter(e.target.value as "" | "Buy" | "Sell")}
                className="input-field w-full"
              >
                <option value="">All</option>
                <option value="Buy">Buy</option>
                <option value="Sell">Sell</option>
              </select>
            </div>
            <div>
              <label className="block text-xs text-gray-400 mb-1">Start Date</label>
              <input
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                className="input-field w-full"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-400 mb-1">End Date</label>
              <input
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                className="input-field w-full"
              />
            </div>
          </div>
          <div className="flex gap-3 mt-4">
            <button onClick={applyFilters} className="btn-primary text-sm">
              Apply Filters
            </button>
            <button
              onClick={clearFilters}
              className="text-sm text-gray-400 hover:text-white transition-colors px-4 py-2"
            >
              Clear
            </button>
          </div>
        </div>
      )}

      {/* Trades Table */}
      <div className="card">
        {isLoading ? (
          <div className="flex items-center justify-center h-40">
            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-accent" />
          </div>
        ) : (
          <>
            <TradeTable trades={tradesData?.items ?? []} />

            {/* Pagination */}
            {tradesData && tradesData.totalPages > 1 && (
              <div className="flex items-center justify-between mt-4 pt-4 border-t border-panel-border">
                <span className="text-sm text-gray-400">
                  Showing {(tradesData.page - 1) * tradesData.pageSize + 1} -{" "}
                  {Math.min(tradesData.page * tradesData.pageSize, tradesData.totalCount)} of{" "}
                  {tradesData.totalCount}
                </span>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() =>
                      setFilters({ ...filters, page: filters.page - 1 })
                    }
                    disabled={filters.page <= 1}
                    className="p-2 rounded-lg hover:bg-gray-800 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>
                  <span className="text-sm text-gray-300 font-mono px-2">
                    {tradesData.page} / {tradesData.totalPages}
                  </span>
                  <button
                    onClick={() =>
                      setFilters({ ...filters, page: filters.page + 1 })
                    }
                    disabled={filters.page >= tradesData.totalPages}
                    className="p-2 rounded-lg hover:bg-gray-800 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
};

export default TradesPage;
