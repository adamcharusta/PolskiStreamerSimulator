using System.Security.Cryptography;
using JasperFx.CommandLine;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PolskiStreamerSimulator.WebApp;

public sealed class OwnerCreateInput : NetCoreInput
{
    [Description("User name of the owner account to create")]
    public string UsernameFlag { get; set; } = "";

    [Description("Replace the existing owner's password, authenticator key and recovery codes")]
    public bool ResetFlag { get; set; }
}

/// <summary>The one-time credentials the command prints; they are not stored in readable form anywhere else.</summary>
public sealed record OwnerCredentials(string Username, string Password, string AuthenticatorKey, string AuthenticatorUri, IReadOnlyList<string> RecoveryCodes);

/// <summary>
/// <c>owner-create</c>: creates the only owner account, or with <c>--reset</c> replaces its credentials, and prints a
/// generated password, authenticator key and ten recovery codes once (spec M4-D §3, TD-69). Nothing secret is read
/// from the command line or the environment.
/// </summary>
[Description("Creates the only owner account for /admin, or resets its credentials.", Name = "owner-create")]
public sealed class OwnerCreateCommand : JasperFxAsyncCommand<OwnerCreateInput>
{
    public const int RecoveryCodeCount = 10;
    private const string Issuer = "Polski Streamer Simulator";
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public override async Task<bool> Execute(OwnerCreateInput input)
    {
        using var host = input.BuildHost();
        await using var scope = host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var created = input.ResetFlag ? await ResetAsync(users) : await CreateAsync(users, input.UsernameFlag);
        if (created.Error is { } error)
        {
            await Console.Error.WriteLineAsync(error);
            return false;
        }

        var credentials = created.Credentials!;
        await Console.Out.WriteLineAsync($"""
            Owner: {credentials.Username}
            Password: {credentials.Password}
            Authenticator key: {credentials.AuthenticatorKey}
            Authenticator URI: {credentials.AuthenticatorUri}
            Recovery codes (each works once):
              {string.Join("\n  ", credentials.RecoveryCodes)}
            Store these now; they are not shown again.
            """);
        return true;
    }

    public static async Task<(OwnerCredentials? Credentials, string? Error)> CreateAsync(UserManager<IdentityUser> users, string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return (null, "--username is required.");
        }

        if (await users.Users.AnyAsync())
        {
            return (null, "An owner account already exists; use --reset to replace its credentials.");
        }

        var user = new IdentityUser(username.Trim());
        var password = NewPassword();
        var result = await users.CreateAsync(user, password);
        return result.Succeeded
            ? (await SecondFactorAsync(users, user, password), null)
            : (null, string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public static async Task<(OwnerCredentials? Credentials, string? Error)> ResetAsync(UserManager<IdentityUser> users)
    {
        var user = await users.Users.SingleOrDefaultAsync();
        if (user is null)
        {
            return (null, "There is no owner account to reset.");
        }

        var password = NewPassword();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            return (null, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return (await SecondFactorAsync(users, user, password), null);
    }

    /// <summary>A new authenticator key and recovery codes; the security stamp changes, so existing sessions end.</summary>
    private static async Task<OwnerCredentials> SecondFactorAsync(UserManager<IdentityUser> users, IdentityUser user, string password)
    {
        await users.ResetAuthenticatorKeyAsync(user);
        await users.SetTwoFactorEnabledAsync(user, true);
        var key = (await users.GetAuthenticatorKeyAsync(user))!;
        var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))!.ToList();
        await users.UpdateSecurityStampAsync(user);
        var uri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(user.UserName!)}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
        return new OwnerCredentials(user.UserName!, password, key, uri, codes);
    }

    private static string NewPassword() => RandomNumberGenerator.GetString(PasswordAlphabet, 24);
}
