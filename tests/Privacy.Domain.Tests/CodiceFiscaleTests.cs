using Privacy.Domain;
using Xunit;

namespace Privacy.Domain.Tests;

// Casi calcolati a mano con le tabelle dell'Agenzia delle Entrate; i codici sono di fantasia o pubblici.
public class CodiceFiscaleTests
{
    [Theory]
    [InlineData("RSSMRA80A01H501U")]      // sedici caratteri con carattere di controllo corretto
    [InlineData("rssmra80a01h501u")]      // maiuscole/minuscole non contano
    [InlineData(" RSSMRA80A01H501U ")]    // spazi tolti
    [InlineData("MRTMTT91D08F205J")]
    [InlineData("00743110157")]           // partita IVA con cifra di controllo corretta
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]                    // facoltativo: vuoto è ammesso
    public void Valid(string? value) => Assert.Null(CodiceFiscale.Check(value));

    [Theory]
    [InlineData("RSSMRA80A01H501X")]      // carattere di controllo sbagliato
    [InlineData("RSSMRA80A01H501")]       // manca un carattere
    [InlineData("RSSMRA80A01H501UU")]     // un carattere di troppo
    [InlineData("RSSMRA80A01H50-U")]      // simbolo non ammesso
    [InlineData("00743110158")]           // partita IVA con cifra di controllo sbagliata
    [InlineData("12345")]
    public void Invalid(string value) => Assert.NotNull(CodiceFiscale.Check(value));

    [Fact] public void CheckChar_MatchesKnownSample() => Assert.Equal('U', CodiceFiscale.CheckChar("RSSMRA80A01H501"));
    [Fact] public void CheckChar_MatchesAnotherSample() => Assert.Equal('J', CodiceFiscale.CheckChar("MRTMTT91D08F205"));

    [Fact]
    public void Lowercase_IsAccepted() => Assert.True(CodiceFiscale.IsValid("mrtmtt91d08f205j"));
}
