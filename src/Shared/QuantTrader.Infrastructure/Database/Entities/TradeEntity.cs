using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantTrader.Infrastructure.Database.Entities;

/// <summary>Persistent entity representing a completed or open trade.</summary>
[Table("trades")]
public sealed class TradeEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(8)]
    [Column("side")]
    public string Side { get; set; } = string.Empty;

    [Column("entry_price", TypeName = "numeric(18,8)")]
    public decimal EntryPrice { get; set; }

    [Column("exit_price", TypeName = "numeric(18,8)")]
    public decimal? ExitPrice { get; set; }

    [Column("quantity", TypeName = "numeric(18,8)")]
    public decimal Quantity { get; set; }

    [Column("realized_pnl", TypeName = "numeric(18,8)")]
    public decimal RealizedPnl { get; set; }

    [Column("commission", TypeName = "numeric(18,8)")]
    public decimal Commission { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("strategy")]
    public string Strategy { get; set; } = string.Empty;

    [Column("entry_time")]
    public DateTimeOffset EntryTime { get; set; }

    [Column("exit_time")]
    public DateTimeOffset? ExitTime { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;
}
