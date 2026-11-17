namespace CoreKit.Identity.Interfaces;

/// <summary>
/// The user-management operations Identity asks its host to authorize. Identity owns this list;
/// the host decides what each operation means in its own authorization model.
/// </summary>
public enum IdentityAdminOperation
{
    ReadUsers = 1,
    CreateUsers = 2,
    UpdateUsers = 3,
    DeleteUsers = 4
}

/// <summary>
/// Decides whether the signed-in caller may perform a user-management operation. Identity owns
/// this port so that it does not need to know how the host models roles or permissions. An
/// operation the authorizer does not recognise must be denied.
/// </summary>
public interface IIdentityAdminAuthorizer
{
    Task<bool> IsAllowedAsync(IdentityAdminOperation operation, CancellationToken ct = default);
}