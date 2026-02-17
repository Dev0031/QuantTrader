namespace QuantTrader.Common.Enums;

/// <summary>Defines the lifecycle states of an exchange order.</summary>
public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Canceled,
    Rejected,
    Expired
}
