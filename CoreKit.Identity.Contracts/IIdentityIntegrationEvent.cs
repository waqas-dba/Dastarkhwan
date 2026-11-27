namespace CoreKit.Identity.Contracts;

/// <summary>
/// Base marker for integration events published by Identity.
/// </summary>
public interface IIdentityIntegrationEvent
{
    DateTime OccurredAt { get; }
}