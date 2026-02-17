using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantTrader.Infrastructure.Database.Entities;

/// <summary>Persistent entity representing a real-time market tick stored in TimescaleDB.</summary>
[Table("market_ticks")]
public sealed class MarketTickEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Column("price", TypeName = "numeric(18,8)")]
    public decimal Price { get; set; }

    [Column("volume", TypeName = "numeric(18,8)")]
    public decimal Volume { get; set; }

    [Column("bid_price", TypeName = "numeric(18,8)")]
    public decimal BidPrice { get; set; }

    [Column("ask_price", TypeName = "numeric(18,8)")]
    public decimal AskPrice { get; set; }

    [Column("timestamp")]
    public DateTimeOffset Timestamp { get; set; }
}
