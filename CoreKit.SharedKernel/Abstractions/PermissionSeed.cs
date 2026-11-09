namespace CoreKit.SharedKernel.Abstractions;

/// <summary>A permission seed: a name and description used when seeding permissions into Identity.</summary>
public sealed record PermissionSeed(string Name, string Description);
