namespace Webhooks.Compliance;

/// <summary>
/// A payload after compliance-approved field-level redaction.
/// Immutable. The original raw bytes do not flow with this record.
/// </summary>
public sealed record RedactedPayload(byte[] Bytes, IReadOnlyList<string> RedactedFieldPaths);

/// <summary>
/// Cross-cutting redaction. Small, stable, justified shared kernel —
/// duplicating this in every module would drift and produce the very
/// inconsistency the kernel exists to prevent.
/// </summary>
public interface IPayloadRedactor
{
    RedactedPayload Redact(byte[] rawJson, PhiSensitivity sensitivity);
}
