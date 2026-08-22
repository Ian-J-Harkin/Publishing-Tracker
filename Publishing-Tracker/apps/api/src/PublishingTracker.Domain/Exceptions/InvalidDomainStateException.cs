namespace PublishingTracker.Domain.Exceptions;

public class InvalidDomainStateException : Exception
{
    public InvalidDomainStateException(string message) : base(message)
    {
    }
}
