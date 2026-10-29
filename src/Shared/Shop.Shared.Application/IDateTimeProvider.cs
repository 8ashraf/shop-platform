namespace Shop.Shared.Application;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
