namespace CoreKit.Identity.Tests;

/// <summary>
/// Mobile numbers are a contact channel, not a login identifier. Email remains the login.
/// Validation rejects missing or badly formed numbers; formatting is normalized away.
/// </summary>
public sealed class MobileValidatorTests
{
    [Theory]
    [InlineData("+15551000001")]
    [InlineData("+44 7700 900123")]
    [InlineData("+44-7700-900-123")]
    [InlineData("(+44) 7700 900123")]
    [InlineData("+44.7700.900123")]
    [InlineData("5551000001")]
    [InlineData("555-100-0001")]
    [InlineData("(555) 100-0001")]
    [InlineData("555.100.0001")]
    public void IsValid_AcceptsFormattedMobileNumbers(string mobile)
    {
        Assert.True(MobileValidator.IsValid(mobile));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-phone")]
    [InlineData("abc")]
    [InlineData("123456")]
    [InlineData("+123456")]
    [InlineData("1234567890123456")]
    [InlineData("+1234567890123456")]
    public void IsValid_RejectsMissingOrInvalidMobileNumbers(string? mobile)
    {
        Assert.False(MobileValidator.IsValid(mobile));
    }

    [Theory]
    [InlineData("+15551000001", "+15551000001")]
    [InlineData("+44 7700 900123", "+447700900123")]
    [InlineData("+44-7700-900-123", "+447700900123")]
    [InlineData("(+44) 7700 900123", "+447700900123")]
    [InlineData("+44.7700.900123", "+447700900123")]
    [InlineData("5551000001", "5551000001")]
    [InlineData("555-100-0001", "5551000001")]
    [InlineData("(555) 100-0001", "5551000001")]
    [InlineData("555.100.0001", "5551000001")]
    public void Normalize_StripsFormatting(string mobile, string expected)
    {
        Assert.Equal(expected, MobileValidator.Normalize(mobile));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-phone")]
    public void Normalize_ReturnsNullForInvalid(string? mobile)
    {
        Assert.Null(MobileValidator.Normalize(mobile));
    }
}
