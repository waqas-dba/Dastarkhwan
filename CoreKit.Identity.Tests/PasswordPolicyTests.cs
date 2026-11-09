namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: the rule that decides whether a password is acceptable to store.
/// Why it matters: a weak password is the easiest way into an account, so the rule must keep
/// working the same way when other code changes around it.
/// The rule under test (see PasswordPolicy): 10 to 256 characters, and not the same as the
/// person's email address. No symbol/digit/capital rules -- length is what counts.
/// </summary>
public sealed class PasswordPolicyTests
{
    private const string Email = "owner@example.com";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_password_is_rejected(string? password)
    {
        var error = PasswordPolicy.Validate(password, Email);

        Assert.Equal("A password is required.", error);
    }

    [Fact]
    public void A_password_one_character_too_short_is_rejected()
    {
        var error = PasswordPolicy.Validate(new string('a', PasswordPolicy.MinLength - 1), Email);

        Assert.NotNull(error);
        Assert.Contains(PasswordPolicy.MinLength.ToString(), error);
    }

    [Fact]
    public void A_password_of_exactly_the_minimum_length_is_accepted()
    {
        var error = PasswordPolicy.Validate(new string('a', PasswordPolicy.MinLength), Email);

        Assert.Null(error);
    }

    [Fact]
    public void A_password_of_exactly_the_maximum_length_is_accepted()
    {
        var error = PasswordPolicy.Validate(new string('a', PasswordPolicy.MaxLength), Email);

        Assert.Null(error);
    }

    [Fact]
    public void A_password_one_character_too_long_is_rejected()
    {
        var error = PasswordPolicy.Validate(new string('a', PasswordPolicy.MaxLength + 1), Email);

        Assert.NotNull(error);
        Assert.Contains(PasswordPolicy.MaxLength.ToString(), error);
    }

    [Fact]
    public void A_password_equal_to_the_email_is_rejected_ignoring_case_and_surrounding_spaces()
    {
        var error = PasswordPolicy.Validate("  Owner@Example.com  ", Email);

        Assert.NotNull(error);
        Assert.Contains("email", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_email_with_surrounding_spaces_and_other_case_is_still_matched_against_the_password()
    {
        var error = PasswordPolicy.Validate("owner@example.com", "  OWNER@example.com ");

        Assert.NotNull(error);
    }

    [Fact]
    public void A_password_different_from_the_email_is_accepted()
    {
        var error = PasswordPolicy.Validate("correct horse battery", Email);

        Assert.Null(error);
    }

    [Fact]
    public void When_no_email_is_given_the_email_check_is_skipped()
    {
        Assert.Null(PasswordPolicy.Validate("abcdefghij", null));
        Assert.Null(PasswordPolicy.Validate("abcdefghij"));
    }

    [Fact]
    public void No_symbol_digit_or_capital_letter_is_required()
    {
        // Length matters more than character-class rules (see the PasswordPolicy summary).
        var error = PasswordPolicy.Validate("abcdefghij", Email);

        Assert.Null(error);
    }
}
