using Npgsql;
using Privacy.Data;
using Xunit;

namespace Privacy.Data.IntegrationTests;

/// <summary>Richiede PRIVACY_TEST_CONN (db/start_test_db.sh). T19: il blocco resiste al riavvio.</summary>
public class AuthTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("PRIVACY_TEST_CONN");
    private const string Good = "frase lunga con parole diverse";

    private static async Task<(AuthService auth, Guid practice, Guid user)> SetupAsync()
    {
        var db = new Db(Conn!);
        await using var c = await db.OpenAsync();
        await using var p = new NpgsqlCommand("INSERT INTO ps.practices (name, controller_name, address, privacy_contact) VALUES ('Studio','Dott.','Via 1','p@example.test') RETURNING id", c);
        var practice = (Guid)(await p.ExecuteScalarAsync())!;
        var auth = new AuthService(db);
        return (auth, practice, await auth.CreateUserAsync(practice, "Medico", "Dott. Rossi", Good));
    }

    private static async Task<long> Count(string sql, params object[] p)
    {
        await using var c = await new Db(Conn!).OpenAsync();
        await using var q = new NpgsqlCommand(sql, c);
        foreach (var v in p) q.Parameters.AddWithValue(v);
        return Convert.ToInt64(await q.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Login_Success_IsCaseInsensitiveOnUsername_AndResetsCounters()
    {
        if (Conn is null) return;
        var (auth, practice, user) = await SetupAsync();
        await auth.LoginAsync(practice, "medico", "password sbagliata!!");
        var ok = await auth.LoginAsync(practice, "  MEDICO ", Good);
        Assert.Equal(LoginOutcome.Success, ok.Outcome);
        Assert.Equal(user, ok.UserId);
        Assert.Equal(0, await Count("SELECT failed_attempts FROM ps.users WHERE id = $1", user));
    }

    [Fact]
    public async Task UnknownUser_And_WrongPassword_AreIndistinguishable()
    {
        if (Conn is null) return;
        var (auth, practice, _) = await SetupAsync();
        var a = await auth.LoginAsync(practice, "inesistente", Good);
        var b = await auth.LoginAsync(practice, "medico", "password sbagliata!!");
        Assert.Equal(a, b);
        Assert.Equal(LoginOutcome.InvalidCredentials, a.Outcome);
    }

    [Fact]
    public async Task Lockout_AfterFiveFailures_PersistsAcrossRestart_AndExpires()
    {
        if (Conn is null) return;
        var (auth, practice, user) = await SetupAsync();
        var t0 = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
        LoginResult last = null!;
        for (var i = 0; i < AuthService.MaxFailedAttempts; i++) last = await auth.LoginAsync(practice, "medico", "password sbagliata!!", t0);
        Assert.Equal(LoginOutcome.Locked, last.Outcome);

        // «riavvio»: nuove istanze, nessuno stato in memoria; anche la password GIUSTA è respinta durante il blocco
        var restarted = new AuthService(new Db(Conn));
        var during = await restarted.LoginAsync(practice, "medico", Good, t0.AddMinutes(5));
        Assert.Equal(LoginOutcome.Locked, during.Outcome);
        Assert.Equal(t0 + AuthService.LockDuration, during.LockedUntil);

        var after = await restarted.LoginAsync(practice, "medico", Good, t0.AddMinutes(16));
        Assert.Equal(LoginOutcome.Success, after.Outcome);
        Assert.Equal(0, await Count("SELECT failed_attempts FROM ps.users WHERE id = $1", user));
    }

    [Fact]
    public async Task DisabledUser_CannotLogin_EvenWithCorrectPassword()
    {
        if (Conn is null) return;
        var (auth, practice, user) = await SetupAsync();
        await using (var c = await new Db(Conn).OpenAsync())
        {
            await using var q = new NpgsqlCommand("UPDATE ps.users SET enabled = false WHERE id = $1", c);
            q.Parameters.AddWithValue(user);
            await q.ExecuteNonQueryAsync();
        }
        Assert.Equal(LoginOutcome.Disabled, (await auth.LoginAsync(practice, "medico", Good)).Outcome);
    }

    [Fact]
    public async Task Audit_RecordsOutcomes_WithoutAnySecret()
    {
        if (Conn is null) return;
        var (auth, practice, user) = await SetupAsync();
        const string wrong = "SEGRETO-DIGITATO-ERRATO";
        await auth.LoginAsync(practice, "medico", wrong);
        await auth.LoginAsync(practice, "medico", Good);
        Assert.Equal(1, await Count("SELECT count(*) FROM ps.audit_events WHERE actor_id = $1 AND action_code = 'login' AND outcome_code = 'failure'", user));
        Assert.Equal(1, await Count("SELECT count(*) FROM ps.audit_events WHERE actor_id = $1 AND action_code = 'login' AND outcome_code = 'success'", user));
        Assert.Equal(0, await Count("SELECT count(*) FROM ps.audit_events a WHERE a::text LIKE '%' || $1 || '%' OR a::text LIKE '%' || $2 || '%'", wrong, Good));
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrent_EnforcesPolicy_UpdatesCredentialsChangedAt()
    {
        if (Conn is null) return;
        var (auth, practice, user) = await SetupAsync();
        Assert.False(await auth.ChangePasswordAsync(user, "password sbagliata!!", "nuova frase lunga e valida"));
        await Assert.ThrowsAsync<ArgumentException>(() => auth.ChangePasswordAsync(user, Good, "corta"));
        var before = await Count("SELECT extract(epoch FROM credentials_changed_at)::bigint FROM ps.users WHERE id = $1", user);
        await Task.Delay(1100);
        Assert.True(await auth.ChangePasswordAsync(user, Good, "nuova frase lunga e valida"));
        Assert.True(await Count("SELECT extract(epoch FROM credentials_changed_at)::bigint FROM ps.users WHERE id = $1", user) > before);
        Assert.Equal(LoginOutcome.InvalidCredentials, (await auth.LoginAsync(practice, "medico", Good)).Outcome);
        Assert.Equal(LoginOutcome.Success, (await auth.LoginAsync(practice, "medico", "nuova frase lunga e valida")).Outcome);
    }

    [Fact]
    public async Task CreateUser_WeakPassword_IsRejected()
    {
        if (Conn is null) return;
        var (auth, practice, _) = await SetupAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => auth.CreateUserAsync(practice, "altro", "Altro", "corta"));
    }

    [Fact]
    public async Task Setup_WeakPassword_CreatesNothing()
    {
        if (Conn is null) return;
        var setup = new SetupService(new Db(Conn));
        var before = await Count("SELECT count(*) FROM ps.practices");
        await Assert.ThrowsAsync<ArgumentException>(() => setup.CreateFirstAsync(new PracticeInput("S", "T", null, "Via 1", "p@example.test", null, null), "medico", "Dott.", "corta"));
        Assert.Equal(before, await Count("SELECT count(*) FROM ps.practices"));   // nessuno studio creato se la password non va bene
    }
}
