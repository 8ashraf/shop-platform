using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Domain;

public sealed class User : Entity
{
    private User() { }

    public Guid Id { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public bool PhoneVerified { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Result<User> Create(string phone, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Result.Failure<User>(IdentityErrors.User.PhoneRequired);
        }

        return Result.Success(new User
        {
            Id = Guid.NewGuid(),
            Phone = phone.Trim(),
            CreatedAt = nowUtc
        });
    }

    public void MarkPhoneVerified() => PhoneVerified = true;
}