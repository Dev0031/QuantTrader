using Microsoft.EntityFrameworkCore;
using QuantTrader.Infrastructure.Database.Entities;

namespace QuantTrader.Infrastructure.Database;

/// <summary>
/// EF Core DbContext for the trading database backed by PostgreSQL with TimescaleDB.
/// Time-series tables (market_ticks, candles) should have hypertables created manually after migration:
///   SELECT create_hypertable('market_ticks', 'timestamp');
///   SELECT create_hypertable('candles', 'open_time');
/// </summary>
public sealed class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options)
        : base(options)
    {
    }

    public DbSet<MarketTickEntity> MarketTicks => Set<MarketTickEntity>();
    public DbSet<CandleEntity> Candles => Set<CandleEntity>();
    public DbSet<TradeEntity> Trades => Set<TradeEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MarketTickEntity configuration
        modelBuilder.Entity<MarketTickEntity>(entity =>
        {
            entity.HasIndex(e => new { e.Symbol, e.Timestamp })
                  .HasDatabaseName("ix_market_ticks_symbol_timestamp");

            // TimescaleDB hypertable — run after migration:
            // SELECT create_hypertable('market_ticks', 'timestamp');
            entity.HasComment("TimescaleDB hypertable on 'timestamp'. Run: SELECT create_hypertable('market_ticks', 'timestamp');");
        });

        // CandleEntity configuration
        modelBuilder.Entity<CandleEntity>(entity =>
        {
            entity.HasIndex(e => new { e.Symbol, e.Interval, e.OpenTime })
                  .HasDatabaseName("ix_candles_symbol_interval_open_time");

            // TimescaleDB hypertable — run after migration:
            // SELECT create_hypertable('candles', 'open_time');
            entity.HasComment("TimescaleDB hypertable on 'open_time'. Run: SELECT create_hypertable('candles', 'open_time');");
        });

        // TradeEntity configuration
        modelBuilder.Entity<TradeEntity>(entity =>
        {
            entity.HasIndex(e => e.Symbol)
                  .HasDatabaseName("ix_trades_symbol");

            entity.HasIndex(e => e.Strategy)
                  .HasDatabaseName("ix_trades_strategy");

            entity.HasIndex(e => e.EntryTime)
                  .HasDatabaseName("ix_trades_entry_time");
        });

        // OrderEntity configuration
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasIndex(e => e.ExchangeOrderId)
                  .HasDatabaseName("ix_orders_exchange_order_id");

            entity.HasIndex(e => e.Symbol)
                  .HasDatabaseName("ix_orders_symbol");

            entity.HasIndex(e => e.CreatedAt)
                  .HasDatabaseName("ix_orders_created_at");
        });
    }
}
