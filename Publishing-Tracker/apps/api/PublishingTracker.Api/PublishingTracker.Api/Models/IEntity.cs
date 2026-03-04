namespace PublishingTracker.Api.Models;

/// <summary>
/// A base interface for all domain entities, ensuring they have an integer Id.
/// Used to constrain generic repositories in C#.
/// </summary>
public interface IEntity
{
    int Id { get; set; }
}
