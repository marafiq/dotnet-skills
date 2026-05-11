namespace Webhooks.Outbound.Domain;

/// <summary>
/// Value object — enforces invariant that the URL is HTTPS at construction.
/// Constructors that can fail return discriminated outcomes; the type itself
/// cannot exist in an invalid state.
/// </summary>
public readonly record struct EndpointUrl
{
    public Uri Uri { get; }

    private EndpointUrl(Uri uri) => Uri = uri;

    public static UrlParseOutcome Create(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new UrlParseOutcome.MalformedUrl(url);
        if (string.IsNullOrEmpty(uri.Host))
            return new UrlParseOutcome.NoHost(url);
        // Loopback exception so demos and tests can round-trip without TLS plumbing.
        // Production receivers MUST be HTTPS.
        var isLoopback = uri.IsLoopback;
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && isLoopback))
            return new UrlParseOutcome.NotHttps(uri.Scheme);
        return new UrlParseOutcome.Created(new EndpointUrl(uri));
    }

    public override string ToString() => Uri.ToString();
}

public abstract record UrlParseOutcome
{
    public sealed record Created(EndpointUrl Url) : UrlParseOutcome;
    public sealed record MalformedUrl(string Raw) : UrlParseOutcome;
    public sealed record NotHttps(string ActualScheme) : UrlParseOutcome;
    public sealed record NoHost(string Raw) : UrlParseOutcome;
    private UrlParseOutcome() { }
}
