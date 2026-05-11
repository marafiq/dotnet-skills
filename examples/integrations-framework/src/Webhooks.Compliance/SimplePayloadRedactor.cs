using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Webhooks.Compliance;

/// <summary>
/// Default redactor: walks JSON, masks values whose key is in a domain-named
/// PHI key set, and tunes severity by sensitivity. Algorithmic, deterministic, testable.
/// </summary>
public sealed class SimplePayloadRedactor : IPayloadRedactor
{
    private static readonly HashSet<string> PhiKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "residentId", "patientId", "mrn", "ssn",
            "firstName", "lastName", "fullName", "dob",
            "address", "phone", "email",
            "diagnosis", "medication", "vitals",
        };

    public RedactedPayload Redact(byte[] rawJson, PhiSensitivity sensitivity)
    {
        if (sensitivity == PhiSensitivity.None || rawJson.Length == 0)
            return new RedactedPayload(rawJson, []);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(rawJson);
        }
        catch (JsonException)
        {
            // Cannot parse — redact entirely under any non-None sensitivity.
            return new RedactedPayload(
                Encoding.UTF8.GetBytes("\"<unparseable, fully redacted>\""),
                ["$"]);
        }

        if (root is null)
            return new RedactedPayload(rawJson, []);

        var redacted = new List<string>();
        Walk(root, "$", sensitivity, redacted);
        var bytes = Encoding.UTF8.GetBytes(root.ToJsonString());
        return new RedactedPayload(bytes, redacted);
    }

    private static void Walk(JsonNode node, string path, PhiSensitivity sensitivity, List<string> redacted)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj.ToList())
                {
                    var childPath = $"{path}.{kvp.Key}";
                    if (PhiKeys.Contains(kvp.Key))
                    {
                        obj[kvp.Key] = MaskValue(kvp.Value, sensitivity);
                        redacted.Add(childPath);
                    }
                    else if (kvp.Value is not null)
                    {
                        Walk(kvp.Value, childPath, sensitivity, redacted);
                    }
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is { } child)
                        Walk(child, $"{path}[{i}]", sensitivity, redacted);
                }
                break;
        }
    }

    private static JsonNode? MaskValue(JsonNode? value, PhiSensitivity sensitivity)
    {
        if (value is null) return null;
        return sensitivity switch
        {
            PhiSensitivity.Restricted => JsonValue.Create("<RESTRICTED>"),
            PhiSensitivity.Standard   => JsonValue.Create("<PHI>"),
            PhiSensitivity.Limited    => JsonValue.Create("<PII>"),
            _                         => value,
        };
    }
}
