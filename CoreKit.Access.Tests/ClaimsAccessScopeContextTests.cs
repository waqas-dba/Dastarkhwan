using CoreKit.Access.Abstractions;
using CoreKit.Access.Services;
using Microsoft.AspNetCore.Http;

namespace CoreKit.Access.Tests;

public sealed class ClaimsAccessScopeContextTests
{
    private static ClaimsAccessScopeContext Create(HttpContext? context)
        => new(new HttpContextAccessor { HttpContext = context });

    [Fact]
    public void Current_ReturnsNull_WhenHttpContextIsMissing()
        => Assert.Null(Create(null).Current);

    [Fact]
    public void Current_ReturnsNull_WhenItemIsMissing()
        => Assert.Null(Create(new DefaultHttpContext()).Current);

    [Fact]
    public void Current_ReturnsTenantScope_WhenItemIsValidGuid()
    {
        var tenantId = Guid.NewGuid();
        var http = new DefaultHttpContext();
        http.Items[AccessScopeContextItemKeys.TenantId] = tenantId;

        var scope = Create(http).Current;

        Assert.NotNull(scope);
        Assert.Equal(AccessScope.TenantType, scope!.Value.Type);
        Assert.Equal(tenantId, scope.Value.Id);
    }

    [Fact]
    public void Current_ReturnsNull_WhenItemIsEmptyGuid()
    {
        var http = new DefaultHttpContext();
        http.Items[AccessScopeContextItemKeys.TenantId] = Guid.Empty;

        Assert.Null(Create(http).Current);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(123)]
    public void Current_ReturnsNull_WhenItemHasInvalidType(object value)
    {
        var http = new DefaultHttpContext();
        http.Items[AccessScopeContextItemKeys.TenantId] = value;

        Assert.Null(Create(http).Current);
    }
}
