using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantTrader.Infrastructure.Database.Entities;

/// <summary>Persistent entity representing an OHLCV candlestick stored in TimescaleDB.</summary>
[Table("candles")]
public sealed class CandleEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column("interval")]
    public string Interval { get; set; } = string.Empty;

    [Column("open", TypeName = "numeric(18,8)")]
    public decimal Open { get; set; }

    [Column("high", TypeName = "numeric(18,8)")]
    public decimal High { get; set; }

    [Column("low", TypeName = "numeric(18,8)")]
    public decimal Low { get; set; }

    [Column("close", TypeName = "numeric(18,8)")]
    public decimal Close { get; set; }

    [Column("volume", TypeName = "numeric(18,8)")]
    public decimal Volume { get; set; }

    [Column("open_time")]
    public DateTimeOffset OpenTime { get; set; }

    [Column("close_time")]
    public DateTimeOffset CloseTime { get; set; }
}
