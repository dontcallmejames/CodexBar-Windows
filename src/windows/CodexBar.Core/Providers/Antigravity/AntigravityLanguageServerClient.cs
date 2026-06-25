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
    private static readonly string[] Schemes = ["https", "http"];

    // Method name -> request body. Ordered: summary first, then legacy fallbacks.
    private static readonly (string Method, string Body)[] Rpcs =
    [
        ("RetrieveUserQuotaSummary", """{"forceRefresh":true}"""),
        ("GetUserStatus", """{"metadata":{"ideName":"antigravity","extensionName":"antigravity","ideVersion":"unknown","locale":"en"}}"""),
        ("GetCommandModelConfigs", """{"metadata":{"ideName":"antigravity","extensionName":"antigravity","ideVersion":"unknown","locale":"en"}}"""),
    ];

    private readonly HttpClient httpClient;

    public AntigravityLanguageServerClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<AntigravityQuotaResponse?> FetchAsync(AntigravityCandidate candidate, CancellationToken cancellationToken)
    {
        foreach (var port in candidate.LoopbackPorts)
        {
            foreach (var scheme in Schemes)
            {
                foreach (var (method, body) in Rpcs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var document = await TryPostAsync(scheme, port, method, body, candidate.CsrfToken, cancellationToken);
                    if (document is not null)
                    {
                        return new AntigravityQuotaResponse(method, document);
                    }
                }
            }
        }

        return null;
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
