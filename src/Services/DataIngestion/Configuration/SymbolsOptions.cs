namespace QuantTrader.DataIngestion;

/// <summary>Holds the list of symbols to track across workers.</summary>
public sealed class SymbolsOptions
{
    public List<string> Symbols { get; set; } = [];
}
