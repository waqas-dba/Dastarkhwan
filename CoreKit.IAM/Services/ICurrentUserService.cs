using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Services;

/// <summary>
/// Who is making this request? Read from the validated token, so it never touches the database.
/// It deliberately knows nothing about tenants, stores or restaurants.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions { get; }

    bool IsAuthenticated { get; }
}
