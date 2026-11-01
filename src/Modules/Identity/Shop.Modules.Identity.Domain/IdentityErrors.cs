using Shop.Shared.Kernel;

namespace Shop.Modules.Identity.Domain;

public static class IdentityErrors
{
    public static class User
    {
        public static readonly Error PhoneRequired =
            new("Identity.User.PhoneRequired", "Phone number is required.");
    }

    public static class Account
    {
        public static readonly Error AlreadyActive =
            new("Identity.Account.AlreadyActive", "Account is already active.");
    }

    public static class Organization
    {
        public static readonly Error NameRequired =
            new("Identity.Organization.NameRequired", "Organization name is required.");

        public static readonly Error NotPending =
            new("Identity.Organization.NotPending", "Only a pending organization can be reviewed.");
    }

    public static class Otp
    {
        public static readonly Error AlreadyUsed =
            new("Identity.Otp.AlreadyUsed", "Code was already used.");

        public static readonly Error Expired =
            new("Identity.Otp.Expired", "Code has expired.");

        public static readonly Error TooManyAttempts =
            new("Identity.Otp.TooManyAttempts", "Too many attempts. Request a new code.");

        public static readonly Error Invalid =
            new("Identity.Otp.Invalid", "Code is incorrect.");
    }
}