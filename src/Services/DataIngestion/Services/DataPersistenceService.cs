using Microsoft.EntityFrameworkCore;
using QuantTrader.Common.Models;
using QuantTrader.Infrastructure.Database;
using QuantTrader.Infrastructure.Database.Entities;

namespace QuantTrader.DataIngestion.Services;

/// <summary>Persists market data (ticks and candles) to TimescaleDB via Entity Framework.</summary>
public interface IDataPersistenceService
{
    /// <summary>Save a single market tick to the database.</summary>
    Task SaveTickAsync(MarketTick tick, CancellationToken cancellationToken = default);

    /// <summary>Save a single candle to the database (upsert by symbol + interval + openTime).</summary>
    Task SaveCandleAsync(Candle candle, CancellationToken cancellationToken = default);

    /// <summary>Bulk-insert a batch of market ticks for efficiency.</summary>
    Task SaveTickBatchAsync(IEnumerable<MarketTick> ticks, CancellationToken cancellationToken = default);
}

public sealed class DataPersistenceService : IDataPersistenceService
{
    private readonly TradingDbContext _dbContext;
    private readonly ILogger<DataPersistenceService> _logger;

    public DataPersistenceService(TradingDbContext dbContext, ILogger<DataPersistenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SaveTickAsync(MarketTick tick, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = MapTickToEntity(tick);
            _dbContext.MarketTicks.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tick for {Symbol} at {Timestamp}", tick.Symbol, tick.Timestamp);
        }
    }

    public async Task SaveCandleAsync(Candle candle, CancellationToken cancellationToken = default)
    {
        try
        {
            // Upsert: check if candle already exists for this symbol + interval + openTime
            var existing = await _dbContext.Candles
                .FirstOrDefaultAsync(c =>
                    c.Symbol == candle.Symbol &&
                    c.Interval == candle.Interval &&
                    c.OpenTime == candle.OpenTime,
                    cancellationToken);

            if (existing is not null)
            {
                // Update existing candle (price may have changed for the current interval)
                existing.Open = candle.Open;
                existing.High = candle.High;
                existing.Low = candle.Low;
                existing.Close = candle.Close;
                existing.Volume = candle.Volume;
                existing.CloseTime = candle.CloseTime;
            }
            else
            {
                var entity = MapCandleToEntity(candle);
                _dbContext.Candles.Add(entity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save candle for {Symbol} ({Interval}) at {OpenTime}",
                candle.Symbol, candle.Interval, candle.OpenTime);
        }
    }

    public async Task SaveTickBatchAsync(IEnumerable<MarketTick> ticks, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = ticks.Select(MapTickToEntity).ToList();

            if (entities.Count == 0)
                return;

            _dbContext.MarketTicks.AddRange(entities);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Persisted batch of {Count} ticks", entities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tick batch ({Count} ticks)", ticks.Count());
        }
    }

    private static MarketTickEntity MapTickToEntity(MarketTick tick)
    {
        return new MarketTickEntity
        {
            Symbol = tick.Symbol,
            Price = tick.Price,
            Volume = tick.Volume,
            BidPrice = tick.BidPrice,
            AskPrice = tick.AskPrice,
            Timestamp = tick.Timestamp
        };
    }

    private static CandleEntity MapCandleToEntity(Candle candle)
    {
        return new CandleEntity
        {
            Symbol = candle.Symbol,
            Interval = candle.Interval,
            Open = candle.Open,
            High = candle.High,
            Low = candle.Low,
            Close = candle.Close,
            Volume = candle.Volume,
            OpenTime = candle.OpenTime,
            CloseTime = candle.CloseTime
        };
    }
}
