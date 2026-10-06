using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Models;


public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = null!;

    public DateTime AccessTokenExpiresAt { get; set; }

    public string RefreshToken { get; set; } = null!;

    public DateTime RefreshTokenExpiresAt { get; set; }

    public UserDto User { get; set; } = null!;
}