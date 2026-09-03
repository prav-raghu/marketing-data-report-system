namespace DotNetMonoRepoTemplate.Types;

public static class DisposableEmailDomains
{
    public static readonly IReadOnlySet<string> Domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "yopmail.com",
        "yopmail.fr",
        "yopmail.net",
        "proton.me",
        "protonmail.com",
        "mailinator.com",
        "guerrillamail.com",
        "guerrillamail.info",
        "guerrillamail.biz",
        "guerrillamail.de",
        "sharklasers.com",
        "10minutemail.com",
        "10minutemail.net",
        "tempmail.com",
        "temp-mail.org",
        "throwawaymail.com",
        "trashmail.com",
        "trashmail.net",
        "getnada.com",
        "dispostable.com",
        "fakeinbox.com",
        "mailnesia.com",
        "maildrop.cc",
        "mintemail.com",
        "moakt.com",
        "emailondeck.com",
        "spamgourmet.com",
    };

    public static bool IsDisposableEmailDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return false;
        }
        var domain = email[(atIndex + 1)..].Trim();
        return Domains.Contains(domain);
    }
}
