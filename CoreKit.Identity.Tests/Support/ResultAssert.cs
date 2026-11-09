namespace CoreKit.Identity.Tests.Support;

/// <summary>Assertions for IdentityResult outcomes shared across test classes.</summary>
internal static class ResultAssert
{
    public static T Ok<T>(IdentityResult<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    public static void Ok(IdentityResult result)
    {
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    public static void Fails(IdentityResult result, IdentityErrorKind kind, string? code = null)
    {
        Assert.True(result.IsFailure, "Expected a failure but the call succeeded.");
        Assert.Equal(kind, result.Error!.Kind);

        if (code is not null)
            Assert.Equal(code, result.Error.Code);
    }

    public static void Fails<T>(IdentityResult<T> result, IdentityErrorKind kind, string? code = null)
    {
        Assert.True(result.IsFailure, "Expected a failure but the call succeeded.");
        Assert.Equal(kind, result.Error!.Kind);

        if (code is not null)
            Assert.Equal(code, result.Error.Code);
    }
}
