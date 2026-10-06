namespace CoreKit.IAM.Models;

public sealed class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}