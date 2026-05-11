namespace Webhooks.Compliance;

/// <summary>
/// Sensitivity classification for redaction policy.
/// Drives what gets masked in delivery logs and inbox storage.
/// </summary>
public enum PhiSensitivity
{
    /// <summary>No PHI; system events, control-plane operations.</summary>
    None,
    /// <summary>PII but not PHI; e.g. partner email addresses.</summary>
    Limited,
    /// <summary>Standard PHI; e.g. resident name + clinical date.</summary>
    Standard,
    /// <summary>Enhanced controls; e.g. resident.deceased.</summary>
    Restricted,
}
