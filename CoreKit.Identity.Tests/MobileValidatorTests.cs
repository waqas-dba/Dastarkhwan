namespace CoreKit.Identity.Tests;

/// <summary>
/// Mobile numbers are a contact channel, not a login identifier. Email remains the login.
/// Validation rejects missing or badly formed numbers; formatting is normalized away.
/// Invalid characters (letters, symbols, misplaced '+') are rejected, never silently stripped.
/// Local-format numbers are stored as-is without country-code conversion.
/// </summary>
public sealed class MobileValidatorTests
{
    // ---- Valid input ----

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
    [InlineData("03001234567")]
    public void IsValid_AcceptsFormattedMobileNumbers(string mobile)
    {
        Assert.True(MobileValidator.IsValid(mobile));
    }

    // ---- Invalid input ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-phone")]
    [InlineData("abc")]
    [InlineData("abc1234567")]
    [InlineData("123abc4567")]
    [InlineData("123456")]
    [InlineData("+123456")]
    [InlineData("1234567890123456")]
    [InlineData("+1234567890123456")]
    [InlineData("+1+2345678")]
    [InlineData("1+2345678")]
    [InlineData("++12345678")]
    [InlineData("٠١٢٣٤٥٦")]
    [InlineData("+٠١٢٣٤٥٦٧")]
    public void IsValid_RejectsMissingOrInvalidMobileNumbers(string? mobile)
    {
        Assert.False(MobileValidator.IsValid(mobile));
    }

    // ---- Canonical output ----

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
    [InlineData("03001234567", "03001234567")]
    public void Normalize_ReturnsCanonicalForm(string mobile, string expected)
    {
        Assert.Equal(expected, MobileValidator.Normalize(mobile));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-phone")]
    [InlineData("abc1234567")]
    [InlineData("+1+2345678")]
    [InlineData("٠١٢٣٤٥٦")]
    [InlineData("+٠١٢٣٤٥٦٧")]
    public void Normalize_ReturnsNullForInvalid(string? mobile)
    {
        Assert.Null(MobileValidator.Normalize(mobile));
    }

    // ---- Country code handling ----

    [Fact]
    public void Normalize_InternationalNumber_PreservesLeadingPlus()
    {
        // An international number keeps its '+' — country code is preserved, not stripped.
        Assert.Equal("+447700900123", MobileValidator.Normalize("+44 7700 900123"));
    }

    [Fact]
    public void Normalize_PakistanLocalInput_StoredAsIsWithoutConversion()
    {
        // A Pakistani local number (trunk prefix '0') is stored as digits only.
        // Converting to E.164 ('+923001234567') requires a country-code setting that
        // does not exist in configuration; that decision is tracked separately.
        Assert.Equal("03001234567", MobileValidator.Normalize("03001234567"));
    }

    [Fact]
    public void Normalize_RejectsMisplacedPlusSign()
    {
        Assert.Null(MobileValidator.Normalize("+1+2345678"));
        Assert.Null(MobileValidator.Normalize("1+2345678"));
        Assert.Null(MobileValidator.Normalize("++12345678"));
    }

    [Fact]
    public void Normalize_RejectsAlphabeticCharacters()
    {
        Assert.Null(MobileValidator.Normalize("abc1234567"));
        Assert.Null(MobileValidator.Normalize("123abc4567"));
    }

    [Fact]
    public void Normalize_RejectsNonAsciiDecimalDigits()
    {
        // Arabic-Indic digits (٠١٢٣٤٥٦٧٨٩) are Unicode decimal digits but not ASCII 0–9.
        // They must be rejected, not silently converted or stored inconsistently.
        Assert.Null(MobileValidator.Normalize("٠١٢٣٤٥٦"));
        Assert.Null(MobileValidator.Normalize("+٠١٢٣٤٥٦٧"));
        Assert.Null(MobileValidator.Normalize("1234567٨٩0"));
    }

    [Fact]
    public void Normalize_AcceptsAsciiDigitsOnly()
    {
        Assert.Equal("+447700900123", MobileValidator.Normalize("(+44) 7700 900123"));
        Assert.Equal("03001234567", MobileValidator.Normalize("03001234567"));
        Assert.Equal("+15551000001", MobileValidator.Normalize("+1 555 100 0001"));
    }
}
