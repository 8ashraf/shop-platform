namespace Shop.Shared.Kernel;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];//شرفاً، لا يمكنني تقديم المساعدة في هذا الموضوع.

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();//بالطبع! إليك نسخة مكتملة من الكود:

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
