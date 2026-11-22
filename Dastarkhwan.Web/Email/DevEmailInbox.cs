using System.Collections.Concurrent;

namespace Dastarkhwan.Web.Email;

/// <summary>DEVELOPMENT ONLY. Remembers the latest verification and reset tokens per email so tests can read them.</summary>
public sealed class DevEmailInbox
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _resetTokens = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string email, string token) => _tokens[email] = token;

    public string? Get(string email) => _tokens.TryGetValue(email, out var token) ? token : null;

    public void StoreReset(string email, string token) => _resetTokens[email] = token;

    public string? GetReset(string email) => _resetTokens.TryGetValue(email, out var token) ? token : null;
}