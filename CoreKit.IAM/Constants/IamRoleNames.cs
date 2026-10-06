using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Constants;

public static class IamRoleNames
{
    /// <summary>Always holds every permission and can never be deleted or renamed.</summary>
    public const string Administrator = "Administrator";

    public const string Manager = "Manager";

    public const string Staff = "Staff";
}
