using CoreKit.Access.Abstractions;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Services;
using CoreKit.Identity.Tests.Support;
using Xunit;

namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: the identity-to-access authorization port maps each user-management
/// operation to exactly one Access platform permission, and denies anything it does not recognise.
/// Why it matters: if the mapping is wrong, the wrong caller could manage users.
/// </summary>
public sealed class AccessBackedIdentityAdminAuthorizerTests
{
    [Theory]
    [InlineData(IdentityAdminOperation.ReadUsers, IdentityAccessPermissionNames.UsersRead)]
    [InlineData(IdentityAdminOperation.CreateUsers, IdentityAccessPermissionNames.UsersCreate)]
    [InlineData(IdentityAdminOperation.UpdateUsers, IdentityAccessPermissionNames.UsersUpdate)]
    [InlineData(IdentityAdminOperation.DeleteUsers, IdentityAccessPermissionNames.UsersDelete)]
    public async Task EachOperation_AsksMatchingPermission(IdentityAdminOperation operation, string expectedPermission)
    {
        var checker = new RecordingPermissionChecker(answer: true);
        var authorizer = new AccessBackedIdentityAdminAuthorizer(checker);

        var result = await authorizer.IsAllowedAsync(operation);

        Assert.True(result);
        Assert.Single(checker.PlatformPermissionsAsked);
        Assert.Equal(expectedPermission, checker.PlatformPermissionsAsked[0]);
    }

    [Theory]
    [InlineData(IdentityAdminOperation.ReadUsers)]
    [InlineData(IdentityAdminOperation.CreateUsers)]
    [InlineData(IdentityAdminOperation.UpdateUsers)]
    [InlineData(IdentityAdminOperation.DeleteUsers)]
    public async Task WhenAccessRefuses_TheOperationIsDenied(IdentityAdminOperation operation)
    {
        var authorizer = new AccessBackedIdentityAdminAuthorizer(new RecordingPermissionChecker(answer: false));

        Assert.False(await authorizer.IsAllowedAsync(operation));
    }

    [Fact]
    public async Task UnrecognisedOperation_IsDenied_WithoutAskingAccess()
    {
        var checker = new RecordingPermissionChecker(answer: true);
        var authorizer = new AccessBackedIdentityAdminAuthorizer(checker);

        Assert.False(await authorizer.IsAllowedAsync((IdentityAdminOperation)999));

        Assert.Empty(checker.PlatformPermissionsAsked);
        Assert.Equal(0, checker.NonPlatformCalls);
    }

    private sealed class RecordingPermissionChecker : IPermissionChecker
    {
        private readonly bool _answer;

        public RecordingPermissionChecker(bool answer) => _answer = answer;

        public List<string> PlatformPermissionsAsked { get; } = new();

        public int NonPlatformCalls { get; private set; }

        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        {
            NonPlatformCalls++;
            return Task.FromResult(_answer);
        }

        public Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
        {
            PlatformPermissionsAsked.Add(permission);
            return Task.FromResult(_answer);
        }
    }
}