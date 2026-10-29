using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using PolskiStreamerSimulator.Infrastructure.Persistence;

namespace PolskiStreamerSimulator.Infrastructure.Security;

/// <summary>
/// The owner's Identity store; recovery codes are kept only as SHA-256 hashes, so a database read cannot reveal an
/// unused one, and redeeming a code removes it (spec M4-D §3).
/// </summary>
public sealed class OwnerUserStore(GameDbContext context, IdentityErrorDescriber? describer = null)
    : UserOnlyStore<IdentityUser, GameDbContext>(context, describer)
{
    // The names Identity's store keeps recovery codes under.
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string RecoveryCodeTokenName = "RecoveryCodes";

    public override Task ReplaceCodesAsync(IdentityUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken = default)
        => base.ReplaceCodesAsync(user, recoveryCodes.Select(Hash), cancellationToken);

    // The base implementation would pass the remaining (already hashed) codes back through ReplaceCodesAsync.
    public override async Task<bool> RedeemCodeAsync(IdentityUser user, string code, CancellationToken cancellationToken = default)
    {
        var hash = Hash(code);
        var stored = (await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "").Split(';');
        if (!stored.Contains(hash, StringComparer.Ordinal))
        {
            return false;
        }

        await base.ReplaceCodesAsync(user, stored.Where(s => s != hash), cancellationToken);
        return true;
    }

    /// <summary>Lowercase hex SHA-256 of the code as typed, trimmed and without dashes, in upper case.</summary>
    public static string Hash(string code)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant())));
}
