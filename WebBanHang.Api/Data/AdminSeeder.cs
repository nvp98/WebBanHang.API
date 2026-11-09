using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data.Entities;

namespace WebBanHang.Api.Data;

/// <summary>
/// <c>dotnet run -- create-admin &lt;username&gt; [password]</c>: creates (or resets) a back-office account in the Admin group.
/// The password is prompted for when omitted, so it does not end up in shell history.
/// </summary>
public static class AdminSeeder
{
    private const int AdminGroupId = 1;

    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: dotnet run -- create-admin <username> [password]");
            Environment.ExitCode = 1;
            return;
        }

        var username = args[0].Trim();
        var password = args.Length > 1 ? args[1] : ReadPassword();
        if (password.Length < 8)
        {
            Console.Error.WriteLine("Password must be at least 8 characters.");
            Environment.ExitCode = 1;
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();

        var logon = await db.Logons.Active().FirstOrDefaultAsync(l => l.Username == username);
        if (logon is null)
        {
            logon = new Logon { Username = username, Password = Passwords.Hash(password), IsActived = 1, IsSeo = 0 };
            await db.InsertAsync(logon, null, CancellationToken.None);
            Console.WriteLine($"Created back-office account '{username}'.");
        }
        else
        {
            logon.Password = Passwords.Hash(password);
            logon.IsActived = 1;
            await db.SaveChangesAsync();
            Console.WriteLine($"Reset password of '{username}'.");
        }

        var logicalId = logon.Id ?? logon.Pk;
        if (!await db.Assigns.Active().AnyAsync(a => a.LogonsId == logicalId && a.GroupsId == AdminGroupId))
            await db.InsertAsync(new Assign { LogonsId = logicalId, GroupsId = AdminGroupId, LanguageId = Lang.Vi }, null, CancellationToken.None);

        await tx.CommitAsync();
    }

    private static string ReadPassword()
    {
        Console.Write("Password: ");
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
            else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string([.. chars]);
    }
}
