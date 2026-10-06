namespace API.SERVICE.Security;

/// <summary>Política de bloqueo; mismos valores que el SqlMembershipProvider del Web.config del ERP.</summary>
public sealed class MembershipOptions
{
    public const string SectionName = "Membership";

    public int MaxInvalidPasswordAttempts { get; set; } = 5;

    public int PasswordAttemptWindowMinutes { get; set; } = 10;
}
