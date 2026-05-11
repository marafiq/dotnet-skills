namespace Webhooks.Outbound.Domain;

/// <summary>
/// Reference to a secret stored elsewhere. The bytes never live in the value object;
/// resolution is the secret store's job. Stops accidental secret-in-log leaks at
/// the type level.
/// </summary>
public readonly record struct SecretRef(string VaultPath)
{
    public override string ToString() => VaultPath;
}

public interface ISecretReader
{
    Task<byte[]> ReadAsync(SecretRef reference, CancellationToken ct);
}
