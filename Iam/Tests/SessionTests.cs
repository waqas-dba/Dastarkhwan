using Iam.Core.Entities;
using Iam.Core.Results;
using Xunit;

namespace Iam.Tests;

public class SessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    private static Session NewSession()
        => Session.Start(Guid.NewGuid(), Guid.NewGuid(), "hash-1", "Chrome", "127.0.0.1", "test", Now, Lifetime);

    [Fact]
    public void Rotating_with_the_current_token_gives_a_new_token_and_extends_the_session()
    {
        var session = NewSession();
        var later = Now.AddDays(1);

        var result = session.Rotate("hash-1", "hash-2", later, Lifetime, sliding: true, Grace);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-2", session.RefreshTokenHash);
        Assert.Equal(later + Lifetime, session.ExpiresAtUtc);
    }

    [Fact]
    public void A_fixed_session_does_not_extend_when_rotated()
    {
        var session = NewSession();
        var originalExpiry = session.ExpiresAtUtc;

        session.Rotate("hash-1", "hash-2", Now.AddDays(1), Lifetime, sliding: false, Grace);

        Assert.Equal(originalExpiry, session.ExpiresAtUtc);
    }

    [Fact]
    public void Replaying_an_old_token_long_after_rotation_closes_the_session()
    {
        var session = NewSession();
        session.Rotate("hash-1", "hash-2", Now, Lifetime, true, Grace);

        var result = session.Rotate("hash-1", "hash-3", Now.AddMinutes(5), Lifetime, true, Grace);

        Assert.True(result.IsFailure);
        Assert.Equal(IamErrors.Sessions.ReuseDetected, result.Error);
        Assert.False(session.IsActive(Now.AddMinutes(5)));
    }

    [Fact]
    public void Replaying_an_old_token_within_the_grace_period_is_treated_as_two_tabs_not_theft()
    {
        var session = NewSession();
        session.Rotate("hash-1", "hash-2", Now, Lifetime, true, Grace);

        var result = session.Rotate("hash-1", "hash-3", Now.AddSeconds(3), Lifetime, true, Grace);

        Assert.True(result.IsFailure);
        Assert.Equal(IamErrors.Sessions.ConcurrentRefresh, result.Error);
        Assert.True(session.IsActive(Now.AddSeconds(3)));
    }

    [Fact]
    public void An_unknown_token_is_rejected_without_closing_the_session()
    {
        var session = NewSession();

        var result = session.Rotate("not-a-real-hash", "hash-2", Now, Lifetime, true, Grace);

        Assert.Equal(IamErrors.Sessions.InvalidToken, result.Error);
        Assert.True(session.IsActive(Now));
    }

    [Fact]
    public void An_expired_session_cannot_be_rotated()
    {
        var session = NewSession();

        var result = session.Rotate("hash-1", "hash-2", Now + Lifetime + TimeSpan.FromSeconds(1), Lifetime, true, Grace);

        Assert.Equal(IamErrors.Sessions.Inactive, result.Error);
    }
}