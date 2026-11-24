namespace CoreKit.Identity.Contracts;

/// <summary>An Identity user was permanently deleted. Other modules clean up their data for this user.</summary>
public sealed record IdentityUserDeleted(Guid UserId, DateTime OccurredAt) : IIdentityIntegrationEvent;