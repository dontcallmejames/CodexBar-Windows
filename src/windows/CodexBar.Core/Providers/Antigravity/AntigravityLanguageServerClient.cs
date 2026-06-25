using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodexBar.Core.Providers.Antigravity;

/// <summary>One quota-bearing RPC response. Owns the parsed document; dispose after mapping.</summary>
public sealed class AntigravityQuotaResponse : IDisposable
{
    public AntigravityQuotaResponse(string method, JsonDocument document)
    {
        Method = method;
        Document = document;
    }

    public string Method { get; }
    public JsonDocument Document { get; }
    public void Dispose() => Document.Dispose();
}

public sealed class AntigravityLanguageServerClient
{
    private const string ServicePath = "/exa.language_server_pb.LanguageServerService/";
    private const string MetadataBody = """{"metadata":{"ideName":"antigravity","extensionName":"antigravity","ideVersion":"unknown","locale":"en"}}""";

    // Method name -> request body. Ordered: summary first, then legacy fallbacks.
    private static readonly (string Method, string Body)[] Rpcs =
    [
        ("RetrieveUserQuotaSummary", """{"forceRefresh":true}"""),
        ("GetUserStatus", MetadataBody),
        ("GetCommandModelConfigs", MetadataBody),
    ];

    private readonly HttpClient httpClient;

    public AntigravityLanguageServerClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<AntigravityQuotaResponse?> FetchAsync(AntigravityCandidate candidate, CancellationToken cancellationToken)
    {
        foreach (var (scheme, port, token) in Endpoints(candidate))
        {
            foreach (var (method, body) in Rpcs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = await TryPostAsync(scheme, port, method, body, token, cancellationToken);
                if (document is not null)
                {
                    return new AntigravityQuotaResponse(method, document);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Fetches a GetUserStatus response (identity: plan tier + account email) across the same
    /// endpoints. Used to backfill identity when quota came from RetrieveUserQuotaSummary.
    /// </summary>
    public async Task<JsonDocument?> FetchUserStatusAsync(AntigravityCandidate candidate, CancellationToken cancellationToken)
    {
        foreach (var (scheme, port, token) in Endpoints(candidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await TryPostAsync(scheme, port, "GetUserStatus", MetadataBody, token, cancellationToken);
            if (document is not null)
            {
                return document;
            }
        }

        return null;
    }

    // Loopback endpoints to probe, in order: each discovered language-server port over https then
    // http, then the extension server (plain http) using its own CSRF token when the IDE exposes one.
    private static IEnumerable<(string Scheme, int Port, string Token)> Endpoints(AntigravityCandidate candidate)
    {
        foreach (var port in candidate.LoopbackPorts)
        {
            yield return ("https", port, candidate.CsrfToken);
            yield return ("http", port, candidate.CsrfToken);
        }

        if (candidate.ExtensionServerPort is int extensionPort)
        {
            yield return ("http", extensionPort, candidate.ExtensionServerCsrfToken ?? candidate.CsrfToken);
        }
    }

    private async Task<JsonDocument?> TryPostAsync(
        string scheme,
        int port,
        string method,
        string body,
        string csrfToken,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"{scheme}://127.0.0.1:{port}{ServicePath}{method}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.TryAddWithoutValidation("X-Codeium-Csrf-Token", csrfToken);
            request.Headers.TryAddWithoutValidation("Connect-Protocol-Version", "1");
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
