using Privacy.Domain;
using Xunit;

namespace Privacy.Domain.Tests;

public class PasswordTests
{
    [Fact] public void Hash_Verify_RoundTrip_AndWrongPasswordFails()
    {
        var v = PasswordHasher.Hash("una password lunga e valida", 1000);
        Assert.True(PasswordHasher.Verify("una password lunga e valida", v));
        Assert.False(PasswordHasher.Verify("una password sbagliata!!", v));
    }

    [Fact] public void SameBothPasswords_DifferentVerifiers_BecauseOfSalt()
        => Assert.NotEqual(PasswordHasher.Hash("stessa password lunga", 1000), PasswordHasher.Hash("stessa password lunga", 1000));

    [Fact] public void Verifier_HasDocumentedFormat_AndNoPlaintext()
    {
        var v = PasswordHasher.Hash("segretissima-password", 1000);
        Assert.Matches(@"^pbkdf2-sha256\$1\$1000\$[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+$", v);
        Assert.DoesNotContain("segretissima", v);
    }

    [Theory] [InlineData("")] [InlineData("garbage")] [InlineData("pbkdf2-sha256$1$x$a$b")] [InlineData("md5$1$1$AA==$AA==")]
    public void MalformedVerifier_NeverVerifies(string verifier) => Assert.False(PasswordHasher.Verify("qualunque", verifier));

    [Fact] public void NeedsRehash_WhenIterationsBelowCurrent()
    {
        Assert.True(PasswordHasher.NeedsRehash(PasswordHasher.Hash("password abbastanza lunga", 1000)));
        Assert.False(PasswordHasher.NeedsRehash(PasswordHasher.Dummy));
    }

    [Fact] public void Policy_RejectsShortRepetitiveAndUsernameContaining()
    {
        Assert.NotEmpty(PasswordPolicy.Check("corta", "medico"));
        Assert.NotEmpty(PasswordPolicy.Check("aaaaaaaaaaaaaaaa", "medico"));
        Assert.NotEmpty(PasswordPolicy.Check("la-password-del-MEDICO-1", "medico"));
        Assert.Empty(PasswordPolicy.Check("frase lunga con parole diverse", "medico"));
    }
}
