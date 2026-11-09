namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: the way passwords are turned into an unreadable "hash" before they are
/// stored, and the way a typed password is later checked against that stored hash.
/// Why it matters: if the hasher stopped salting, accepted a wrong password, or never asked for
/// weak old hashes to be upgraded, a database leak would expose many more accounts.
/// Most tests use a low iteration count so they stay fast; the real default (210,000) is checked
/// separately. The count only changes how slow guessing is, not how the checks behave.
/// </summary>
public sealed class PasswordHasherTests
{
    private const int FastIterations = 1_000;
    private const string Password = "correct horse battery staple";

    private static PasswordHasher FastHasher() => new(FastIterations);

    [Fact]
    public void The_hash_is_not_the_plain_password()
    {
        var hash = FastHasher().Hash(Password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash);
    }

    [Fact]
    public void Hashing_the_same_password_twice_gives_different_hashes_because_each_has_its_own_salt()
    {
        var hasher = FastHasher();

        Assert.NotEqual(hasher.Hash(Password), hasher.Hash(Password));
    }

    [Fact]
    public void The_correct_password_is_accepted()
    {
        var hasher = FastHasher();
        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Success, hasher.Verify(hash, Password));
    }

    [Fact]
    public void A_wrong_password_is_rejected()
    {
        var hasher = FastHasher();
        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hash, "a different password"));
    }

    [Fact]
    public void Passwords_are_case_sensitive()
    {
        var hasher = FastHasher();
        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hash, Password.ToUpperInvariant()));
    }

    [Fact]
    public void An_empty_stored_hash_never_matches()
    {
        Assert.Equal(PasswordCheckResult.Failed, FastHasher().Verify(string.Empty, Password));
    }

    [Fact]
    public void An_empty_typed_password_never_matches()
    {
        var hasher = FastHasher();
        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Failed, hasher.Verify(hash, string.Empty));
    }

    [Fact]
    public void Hashing_an_empty_password_is_refused()
    {
        var hasher = FastHasher();

        Assert.ThrowsAny<ArgumentException>(() => hasher.Hash(string.Empty));
        Assert.ThrowsAny<ArgumentException>(() => hasher.Hash(null!));
    }

    [Fact]
    public void A_hash_made_by_another_hasher_with_the_same_settings_is_accepted()
    {
        // Several servers (or a restart) must be able to check each other's hashes.
        var hash = new PasswordHasher(FastIterations).Hash(Password);

        Assert.Equal(PasswordCheckResult.Success, new PasswordHasher(FastIterations).Verify(hash, Password));
    }

    [Fact]
    public void A_correct_password_on_a_weaker_old_hash_is_accepted_but_flagged_for_rehash()
    {
        // The hash was made with fewer iterations than the hasher now requires. The person
        // is let in, and the caller is told to save a fresh, stronger hash.
        var oldHash = new PasswordHasher(FastIterations).Hash(Password);

        var result = new PasswordHasher(FastIterations * 2).Verify(oldHash, Password);

        Assert.Equal(PasswordCheckResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void A_wrong_password_on_a_weaker_old_hash_is_still_rejected()
    {
        var oldHash = new PasswordHasher(FastIterations).Hash(Password);

        var result = new PasswordHasher(FastIterations * 2).Verify(oldHash, "not the password");

        Assert.Equal(PasswordCheckResult.Failed, result);
    }

    [Fact]
    public void The_default_iteration_count_is_the_recommended_minimum()
    {
        Assert.Equal(210_000, PasswordHasher.DefaultIterationCount);
    }

    [Fact]
    public void The_default_hasher_round_trips_a_password()
    {
        // One slow check on purpose: it proves the real production settings work end to end.
        var hasher = new PasswordHasher();
        var hash = hasher.Hash(Password);

        Assert.Equal(PasswordCheckResult.Success, hasher.Verify(hash, Password));
    }

    [Theory]
    [InlineData("pässwörd-密碼-🔑-long-enough")]
    public void Passwords_with_non_english_characters_round_trip(string password)
    {
        var hasher = FastHasher();

        Assert.Equal(PasswordCheckResult.Success, hasher.Verify(hasher.Hash(password), password));
    }

    [Fact]
    public void A_password_of_the_maximum_allowed_length_round_trips()
    {
        var hasher = FastHasher();
        var password = new string('x', PasswordPolicy.MaxLength);

        Assert.Equal(PasswordCheckResult.Success, hasher.Verify(hasher.Hash(password), password));
    }
}
