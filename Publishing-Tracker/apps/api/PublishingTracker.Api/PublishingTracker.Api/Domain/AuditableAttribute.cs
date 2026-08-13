namespace PublishingTracker.Domain;

/// <summary>
/// A custom attribute used to mark properties that should be automatically audited (timestamped)
/// when an entity is saved to the database. Demonstrated C# custom attributes and reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class AuditableAttribute : Attribute
{
    public bool IsCreationTime { get; }

    public AuditableAttribute(bool isCreationTime = false)
    {
        IsCreationTime = isCreationTime;
    }
}
