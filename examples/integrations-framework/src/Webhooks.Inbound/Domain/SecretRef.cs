namespace Webhooks.Inbound.Domain;

/// <summary>
/// Inbound-owned SecretRef — separate type from Outbound's. Avoids cross-module
/// dependency on Outbound for a value object that means the same thing in two
/// different bounded contexts.
/// </summary>
public readonly record struct SecretRef(string VaultPath)
{
    public override string ToString() => VaultPath;
}

public interface ISecretReader
{
    Task<byte[]> ReadAsync(SecretRef reference, CancellationToken ct);
}
