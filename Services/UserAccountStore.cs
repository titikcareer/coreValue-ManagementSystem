namespace main.Services;
public static class Roles
{
    public const string DashboardAccess = "DashboardAccess";
}
public record UserAccount(
    string Nim,
    string Name,
    string Department,
    string PasswordHash,
    IReadOnlyList<string> Roles,
    bool MustChangePassword = true
);
public class UserAccountStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, UserAccount> _usersByNim;
    private readonly Dictionary<string, (int FailedCount, DateTime LockedUntilUtc)> _loginAttempts = new();
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public UserAccountStore()
    {
        _usersByNim = new List<UserAccount>
        {
            CreateUser(
                "011915",
                "Fajri Kurniawan",
                "GA, CSR, KM, SECURITY",
                new[] { Roles.DashboardAccess }),

            CreateUser(
                "011916",
                "Suarsono",
                "GA, CSR, KM, SECURITY",
                new[] { Roles.DashboardAccess }),

            CreateUser(
                "011917",
                "M. Amirudin",
                "GA, CSR, KM, SECURITY",
                Array.Empty<string>()),

            CreateUser(
                "011918",
                "Sri Haryanto",
                "Digital Transformation",
                Array.Empty<string>())
        }.ToDictionary(
            u => u.Nim,
            StringComparer.OrdinalIgnoreCase);
    }

    private static UserAccount CreateUser(
        string nim,
        string name,
        string department,
        IReadOnlyList<string> roles)
    {
        var initialPassword = GenerateInitialPassword(nim);

        return new UserAccount(
            nim,
            name,
            department,
            PasswordHasher.Hash(initialPassword),
            roles,
            MustChangePassword: true);
    }
    private static string GenerateInitialPassword(string nim)
    {
        if (string.IsNullOrWhiteSpace(nim) ||
            nim.Length != 6 ||
            !nim.All(char.IsDigit))
        {
            throw new ArgumentException(
                "NIM must contain exactly 6 digits.",
                nameof(nim));
        }

        return string.Concat(
            nim.AsSpan(2, 2),
            nim.AsSpan(4, 2),
            nim.AsSpan(0, 2));
    }

    public UserAccount? TryAuthenticate(string nim, string password)
    {
        if (string.IsNullOrWhiteSpace(nim) ||
            string.IsNullOrEmpty(password))
        {
            return null;
        }

        lock (_lock)
        {
            if (_loginAttempts.TryGetValue(nim, out var attempt) &&
                attempt.LockedUntilUtc > DateTime.UtcNow)
            {
                return null;
            }

            if (!_usersByNim.TryGetValue(nim, out var user) ||
                !PasswordHasher.Verify(password, user.PasswordHash))
            {
                RegisterFailedAttempt(nim);
                return null;
            }

            _loginAttempts.Remove(nim);
            return user;
        }
    }
    private void RegisterFailedAttempt(string nim)
    {
        var current = _loginAttempts.TryGetValue(nim, out var existing)
            ? existing.FailedCount
            : 0;

        var count = current + 1;

        var lockedUntil = count >= MaxFailedAttempts
            ? DateTime.UtcNow.Add(LockoutDuration)
            : DateTime.MinValue;

        _loginAttempts[nim] = (count, lockedUntil);
    }
    public UserAccount? FindByNim(string nim)
    {
        if (string.IsNullOrWhiteSpace(nim))
        {
            return null;
        }

        lock (_lock)
        {
            return _usersByNim.TryGetValue(nim, out var user)
                ? user
                : null;
        }
    }

    public bool ChangePassword(
        string nim,
        string currentPassword,
        string newPassword)
    {
        if (string.IsNullOrWhiteSpace(nim) ||
            string.IsNullOrEmpty(currentPassword) ||
            string.IsNullOrEmpty(newPassword))
        {
            return false;
        }

        lock (_lock)
        {
            if (!_usersByNim.TryGetValue(nim, out var user))
            {
                return false;
            }

            if (!PasswordHasher.Verify(
                    currentPassword,
                    user.PasswordHash))
            {
                return false;
            }

            _usersByNim[nim] = user with
            {
                PasswordHash = PasswordHasher.Hash(newPassword),
                MustChangePassword = false
            };

            return true;
        }
    }
}