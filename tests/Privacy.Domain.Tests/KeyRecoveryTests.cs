using Privacy.Domain;
using Xunit;

namespace Privacy.Domain.Tests;

public class KeyRecoveryTests
{
    private static readonly byte[] Secret = Enumerable.Range(1, 64).Select(i => (byte)i).ToArray();
    private const string Pass = "una frase segreta molto lunga";

    [Fact] public void RoundTrip() => Assert.Equal(Secret, KeyRecovery.Unwrap(Pass, KeyRecovery.Wrap(Pass, Secret, 2000)));

    [Fact] public void WrongPassphrase_AlteredFile_AndGarbage_AreRejected()
    {
        var file = KeyRecovery.Wrap(Pass, Secret, 2000);
        Assert.Throws<KeyRecoveryException>(() => KeyRecovery.Unwrap("un'altra frase segreta lunga", file));
        var tampered = (byte[])file.Clone(); tampered[^1] ^= 1;
        Assert.Throws<KeyRecoveryException>(() => KeyRecovery.Unwrap(Pass, tampered));
        var header = (byte[])file.Clone(); header[30] ^= 1;   // nonce alterato
        Assert.Throws<KeyRecoveryException>(() => KeyRecovery.Unwrap(Pass, header));
        Assert.Throws<KeyRecoveryException>(() => KeyRecovery.Unwrap(Pass, new byte[10]));
    }

    [Fact] public void ShortPassphrase_IsRefused() => Assert.Throws<ArgumentException>(() => KeyRecovery.Wrap("corta", Secret));

    [Fact] public void SameInput_GivesDifferentFiles_AndNoPlaintextInside()
    {
        var a = KeyRecovery.Wrap(Pass, Secret, 2000); var b = KeyRecovery.Wrap(Pass, Secret, 2000);
        Assert.NotEqual(a, b);
        Assert.False(a.AsSpan().IndexOf(Secret.AsSpan(0, 16)) >= 0);
    }
}
