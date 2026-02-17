using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuantTrader.Infrastructure.Database.Entities;

/// <summary>Persistent entity representing an exchange order with full lifecycle tracking.</summary>
[Table("orders")]
public sealed class OrderEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [MaxLength(128)]
    [Column("exchange_order_id")]
    public string? ExchangeOrderId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(8)]
    [Column("side")]
    public string Side { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("quantity", TypeName = "numeric(18,8)")]
    public decimal Quantity { get; set; }

    [Column("price", TypeName = "numeric(18,8)")]
    public decimal? Price { get; set; }

    [Column("stop_price", TypeName = "numeric(18,8)")]
    public decimal? StopPrice { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("filled_quantity", TypeName = "numeric(18,8)")]
    public decimal FilledQuantity { get; set; }

    [Column("filled_price", TypeName = "numeric(18,8)")]
    public decimal FilledPrice { get; set; }

    [Column("commission", TypeName = "numeric(18,8)")]
    public decimal Commission { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
