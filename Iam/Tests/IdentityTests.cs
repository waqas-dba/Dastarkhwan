using Iam.Core.Identity;
using Xunit;

namespace Iam.Tests;

public class IdentityTests
{
    [Theory]
    [InlineData("03001234567", "+923001234567")]
    [InlineData("0300 1234567", "+923001234567")]
    [InlineData("+92 300 1234567", "+923001234567")]
    [InlineData("923001234567", "+923001234567")]
    [InlineData("3001234567", "+923001234567")]
    [InlineData("0092-300-1234567", "+923001234567")]
    public void Different_spellings_of_one_phone_number_become_the_same_value(string input, string expected)
    {
        Assert.True(PhoneNumber.TryCreate(input, "92", out var phone));
        Assert.Equal(expected, phone.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("1234567890123456789")]
    public void Invalid_phone_numbers_are_rejected(string input)
    {
        Assert.False(PhoneNumber.TryCreate(input, "92", out _));
    }

    [Theory]
    [InlineData("  Ali@Example.COM ", "ali@example.com")]
    [InlineData("a@b.pk", "a@b.pk")]
    public void Emails_are_trimmed_and_lower_cased(string input, string expected)
    {
        Assert.True(EmailAddress.TryCreate(input, out var email));
        Assert.Equal(expected, email.Value);
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@b.pk")]
    [InlineData("a@")]
    [InlineData("a@b")]
    [InlineData("a b@c.pk")]
    public void Invalid_emails_are_rejected(string input)
    {
        Assert.False(EmailAddress.TryCreate(input, out _));
    }

    [Fact]
    public void Login_identifier_decides_between_email_and_phone()
    {
        Assert.True(LoginIdentifier.TryParse("ali@example.com", "92", out var email));
        Assert.Equal(LoginIdentifierKind.Email, email.Kind);

        Assert.True(LoginIdentifier.TryParse("0300-1234567", "92", out var phone));
        Assert.Equal(LoginIdentifierKind.Phone, phone.Kind);
        Assert.Equal("+923001234567", phone.NormalizedValue);
    }
}