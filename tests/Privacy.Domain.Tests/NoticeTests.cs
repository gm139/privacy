using Privacy.Domain;
using Xunit;

namespace Privacy.Domain.Tests;

public class NoticeTests
{
    private static readonly Dictionary<string, string> Values = new() { ["practice_name"] = "Studio Rossi", ["address"] = "Via Roma 1" };

    [Fact] public void Resolve_ReplacesAllowedVariables()
        => Assert.Equal("Studio Rossi, Via Roma 1", TemplateResolver.Resolve("{{practice_name}}, {{ address }}", Values));

    [Fact] public void Resolve_UnknownVariable_Throws()
        => Assert.Throws<TemplateException>(() => TemplateResolver.Resolve("{{password}}", Values));

    [Fact] public void Resolve_MissingValue_Throws()
        => Assert.Throws<TemplateException>(() => TemplateResolver.Resolve("{{phone}}", Values));

    [Fact] public void Render_IsDeterministic_AndHmacDependsOnKeyAndContent()
    {
        var input = new NoticeRenderInput("Testo\r\ninformativa", 2, "PS-ABC", "Mario Rossi", new DateOnly(2026, 5, 4), null, null);
        var t1 = NoticeDocument.Render(input);
        Assert.Equal(t1, NoticeDocument.Render(input));
        Assert.DoesNotContain('\r', t1);
        var k1 = new byte[32]; var k2 = new byte[32]; k2[0] = 1;
        Assert.Equal(NoticeDocument.Hmac(k1, t1), NoticeDocument.Hmac(k1, t1));
        Assert.NotEqual(NoticeDocument.Hmac(k1, t1), NoticeDocument.Hmac(k2, t1));
        Assert.NotEqual(NoticeDocument.Hmac(k1, t1), NoticeDocument.Hmac(k1, NoticeDocument.Render(input with { SubjectName = "Altro" })));
    }

    [Fact] public void Render_DoesNotInventSignatory()
        => Assert.DoesNotContain("Firmatario", NoticeDocument.Render(new NoticeRenderInput("T", 1, "PS-X", "Mario", new DateOnly(2026, 1, 1), null, null)));

    [Fact] public void DeliveryCode_HasExpectedFormat()
        => Assert.Matches("^PS-[0-9A-F]{12}$", NoticeDocument.NewDeliveryCode());
}
