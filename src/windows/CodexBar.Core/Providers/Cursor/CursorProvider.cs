using System.Net;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Cursor;

public sealed class CursorProvider : IUsageProvider
{
    private static readonly Uri UsageSummaryUri = new("https://cursor.com/api/usage-summary");
    private static readonly Uri AccountUri = new("https://cursor.com/api/auth/me");

    private readonly HttpClient httpClient;
    private readonly string? manualCookieHeader;

    public CursorProvider(HttpClient httpClient, string? manualCookieHeader)
    {
        this.httpClient = httpClient;
        this.manualCookieHeader = manualCookieHeader;
    }

    public UsageProvider Provider => UsageProvider.Cursor;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(manualCookieHeader))
        {
            return UsageSnapshot.MissingCredentials(
                UsageProvider.Cursor,
                "Cursor",
                "Cursor cookie header was not found. Add it in Settings.");
        }

        using var usage = await GetJsonAsync(UsageSummaryUri, cancellationToken);
        if (usage is null)
        {
            return UsageSnapshot.RequiresAuthentication(
                UsageProvider.Cursor,
                "Cursor",
                "Cursor rejected your saved cookie. Sign in at cursor.com, copy the Cookie header (must include WorkosCursorSessionToken), and re-paste it in Settings.");
        }

        using var account = await GetJsonAsync(AccountUri, cancellationToken);
        return CursorUsageMapper.Map(usage.RootElement, account?.RootElement, DateTimeOffset.Now);
    }

    private async Task<JsonDocument?> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Cookie", manualCookieHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
