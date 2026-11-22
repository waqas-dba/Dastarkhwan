namespace CoreKit.Identity.IntegrationEvents;

/// <summary>An Identity user was permanently deleted. Other modules clean up their data for this user.</summary>
public sealed record IdentityUserDeleted(Guid UserId, DateTime OccurredAt) : CoreKit.Identity.Interfaces.IIdentityIntegrationEvent;