using Iam.Core.Entities;
using Iam.Core.Identity;
using Iam.Core.Results;
using Xunit;

namespace Iam.Tests;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private static User NewUser()
    {
        PhoneNumber.TryCreate("03001234567", "92", out var phone);
        return User.Create("Test User", null, phone, "hash", Now).Value;
    }

    [Fact]
    public void A_user_needs_an_email_or_a_phone()
    {
        var result = User.Create("Test", null, null, "hash", Now);

        Assert.True(result.IsFailure);
        Assert.Equal(IamErrors.Users.ContactRequired, result.Error);
    }

    [Fact]
    public void Five_failed_logins_lock_the_account_for_the_lockout_period()
    {
        var user = NewUser();

        for (var i = 0; i < 5; i++)
            user.RegisterFailedLogin(Now, maxAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.True(user.IsLockedOut(Now));
        Assert.True(user.IsLockedOut(Now.AddMinutes(14)));
        Assert.False(user.IsLockedOut(Now.AddMinutes(16)));
    }

    [Fact]
    public void A_successful_login_clears_the_failed_attempts()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15));

        user.RegisterSuccessfulLogin(Now);

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Equal(Now, user.LastLoginAtUtc);
    }

    [Fact]
    public void Changing_the_password_changes_the_security_stamp()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.ChangePasswordHash("new-hash", Now);

        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public void A_disabled_user_cannot_sign_in_and_loses_existing_tokens()
    {
        var user = NewUser();
        var before = user.SecurityStamp;

        user.Disable(Now);

        Assert.False(user.CanSignIn(Now));
        Assert.NotEqual(before, user.SecurityStamp);
    }

    [Fact]
    public void Assigning_the_same_role_twice_does_nothing_the_second_time()
    {
        var user = NewUser();
        var role = Role.Create("Cashier", null, false, Now).Value;

        Assert.True(user.AssignRole(role, Now));
        Assert.False(user.AssignRole(role, Now));
        Assert.Single(user.UserRoles);
    }
}