using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Interfaces;


public enum PasswordCheckResult
{
    Failed = 0,
    Success = 1,

    /// <summary>The password is right, but the stored hash uses weaker settings. Save a fresh hash.</summary>
    SuccessRehashNeeded = 2
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheckResult Verify(string passwordHash, string password);
}