using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QuantTrader.Infrastructure.KeyVault;

/// <summary>Abstraction for retrieving secrets from a secure store.</summary>
public interface ISecretProvider
{
    /// <summary>Retrieves a secret value by name.</summary>
    Task<string> GetSecretAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Azure Key Vault implementation of <see cref="ISecretProvider"/> with fallback
/// to <see cref="IConfiguration"/> for local development scenarios.
/// </summary>
public sealed class KeyVaultService : ISecretProvider
{
    private readonly SecretClient? _secretClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeyVaultService> _logger;

    /// <summary>
    /// Creates a new KeyVaultService. When <paramref name="vaultUri"/> is provided and reachable,
    /// secrets are fetched from Azure Key Vault. Otherwise, falls back to IConfiguration.
    /// </summary>
    public KeyVaultService(
        IConfiguration configuration,
        ILogger<KeyVaultService> logger,
        Uri? vaultUri = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (vaultUri is not null)
        {
            try
            {
                _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
                _logger.LogInformation("KeyVault client initialized for {VaultUri}", vaultUri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize KeyVault client for {VaultUri}. Falling back to IConfiguration.", vaultUri);
                _secretClient = null;
            }
        }
        else
        {
            _logger.LogInformation("No KeyVault URI configured. Using IConfiguration as secret provider.");
        }
    }

    public async Task<string> GetSecretAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_secretClient is not null)
        {
            try
            {
                var response = await _secretClient.GetSecretAsync(name, cancellationToken: ct).ConfigureAwait(false);
                return response.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve secret {SecretName} from KeyVault. Falling back to IConfiguration.", name);
            }
        }

        // Fallback: look up in IConfiguration using the secret name as the key.
        // Supports both flat keys ("MySecret") and section-based ("Secrets:MySecret").
        var value = _configuration[name]
                    ?? _configuration[$"Secrets:{name}"];

        if (value is null)
        {
            throw new KeyNotFoundException($"Secret '{name}' not found in KeyVault or IConfiguration.");
        }

        return value;
    }
}
