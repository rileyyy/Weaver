using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class CustomFieldDefinitionTests
{
    private static readonly Guid Room = Guid.NewGuid();

    private static CustomFieldDefinition Field(CustomFieldKind kind, params string[] options) =>
        CustomFieldDefinition.Create(Room, "Field", kind, order: 0, options);

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static string? Normalized(CustomFieldDefinition field, string json) =>
        field.Normalize(Json(json))?.GetRawText();

    [Test]
    public void Text_IsTrimmed()
    {
        Assert.That(Normalized(Field(CustomFieldKind.Text), "\"  Oak  \""), Is.EqualTo("\"Oak\""));
    }

    [TestCase("\"\"")]
    [TestCase("\"   \"")]
    [TestCase("null")]
    public void ABlankOrNullValue_ClearsTheField(string json)
    {
        Assert.That(Normalized(Field(CustomFieldKind.Text), json), Is.Null);
    }

    [TestCase("12")]
    [TestCase("true")]
    [TestCase("[\"a\"]")]
    public void Text_OfAnotherKind_IsRejected(string json)
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Text).Normalize(Json(json)));
    }

    [Test]
    public void Text_LongerThanTheLimit_IsRejected()
    {
        var text = JsonSerializer.Serialize(new string('x', CustomFieldDefinition.TextMaxLength + 1));

        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Text).Normalize(Json(text)));
    }

    [Test]
    public void Markdown_KeepsLineBreaksAndAllowsLongerText()
    {
        var markdown = JsonSerializer.Serialize("- [ ] tiles\n- [x] grout" + new string('x', 1000));

        Assert.That(Normalized(Field(CustomFieldKind.Markdown), markdown), Is.EqualTo(markdown));
    }

    [TestCase("12", "12")]
    [TestCase("-3.25", "-3.25")]
    [TestCase("1.50", "1.5")]
    public void Number_IsStoredAsANumber(string json, string expected)
    {
        Assert.That(Normalized(Field(CustomFieldKind.Number), json), Is.EqualTo(expected));
    }

    [TestCase("\"12\"")]
    [TestCase("true")]
    public void Number_OfAnotherKind_IsRejected(string json)
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Number).Normalize(Json(json)));
    }

    [TestCase("1200", "1200")]
    [TestCase("19.90", "19.9")]
    [TestCase("0.05", "0.05")]
    public void Currency_IsAnAmountWithAtMostTwoDecimals(string json, string expected)
    {
        Assert.That(Normalized(Field(CustomFieldKind.Currency), json), Is.EqualTo(expected));
    }

    [Test]
    public void Currency_WithMoreThanTwoDecimals_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Currency).Normalize(Json("1.005")));
    }

    [Test]
    public void Date_IsACalendarDate()
    {
        Assert.That(Normalized(Field(CustomFieldKind.Date), "\"2026-12-24\""), Is.EqualTo("\"2026-12-24\""));
    }

    [TestCase("\"24/12/2026\"")]
    [TestCase("\"2026-02-30\"")]
    [TestCase("\"2026-12-24T10:00:00Z\"")]
    [TestCase("20261224")]
    public void Date_NotAsYearMonthDay_IsRejected(string json)
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Date).Normalize(Json(json)));
    }

    [TestCase("true")]
    [TestCase("false")]
    public void Checkbox_IsTrueOrFalse(string json)
    {
        Assert.That(Normalized(Field(CustomFieldKind.Checkbox), json), Is.EqualTo(json));
    }

    [Test]
    public void Checkbox_OfAnotherKind_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Checkbox).Normalize(Json("\"yes\"")));
    }

    [TestCase("\" https://example.com/pr/12 \"", "\"https://example.com/pr/12\"")]
    [TestCase("\"http://nas.local/plans\"", "\"http://nas.local/plans\"")]
    public void Url_IsAnAbsoluteWebAddress(string json, string expected)
    {
        Assert.That(Normalized(Field(CustomFieldKind.Url), json), Is.EqualTo(expected));
    }

    [TestCase("\"example.com\"")]
    [TestCase("\"ftp://example.com/file\"")]
    [TestCase("\"javascript:alert(1)\"")]
    public void Url_NotAnHttpAddress_IsRejected(string json)
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Url).Normalize(Json(json)));
    }

    [Test]
    public void User_IsAUserId()
    {
        var id = Guid.NewGuid();

        Assert.That(Normalized(Field(CustomFieldKind.User), $"\"{id.ToString().ToUpperInvariant()}\""), Is.EqualTo($"\"{id}\""));
    }

    [TestCase("\"riley\"")]
    [TestCase("42")]
    public void User_NotAnId_IsRejected(string json)
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.User).Normalize(Json(json)));
    }

    [Test]
    public void Create_GivesEachOptionAnId_InTheOrderGiven()
    {
        var field = Field(CustomFieldKind.SingleSelect, " Oak ", "Pine");

        Assert.Multiple(() =>
        {
            Assert.That(field.Options.Select(o => o.Name), Is.EqualTo(new[] { "Oak", "Pine" }));
            Assert.That(field.Options.Select(o => o.Id).Distinct().Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public void Create_WithTwoOptionsOfTheSameName_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.MultiSelect, "Oak", "oak"));
    }

    [Test]
    public void Create_WithABlankOption_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.SingleSelect, "Oak", " "));
    }

    [Test]
    public void Create_OptionsOnAFieldThatIsNotASelect_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Field(CustomFieldKind.Text, "Oak"));
    }

    [Test]
    public void SingleSelect_IsTheIdOfOneOfItsOptions()
    {
        var field = Field(CustomFieldKind.SingleSelect, "Oak", "Pine");
        var pine = field.Options[1].Id;

        Assert.That(Normalized(field, $"\"{pine}\""), Is.EqualTo($"\"{pine}\""));
    }

    [Test]
    public void SingleSelect_WithAnUnknownOption_IsRejected()
    {
        var field = Field(CustomFieldKind.SingleSelect, "Oak", "Pine");

        Assert.Throws<DomainValidationException>(() => field.Normalize(Json($"\"{Guid.NewGuid()}\"")));
    }

    [Test]
    public void MultiSelect_IsStoredInOptionOrder_WithoutRepeats()
    {
        var field = Field(CustomFieldKind.MultiSelect, "Oak", "Pine", "Birch");
        var (oak, birch) = (field.Options[0].Id, field.Options[2].Id);

        Assert.That(Normalized(field, $"[\"{birch}\", \"{oak}\", \"{birch}\"]"), Is.EqualTo($"[\"{oak}\",\"{birch}\"]"));
    }

    [Test]
    public void MultiSelect_WithNoOptionsChosen_ClearsTheField()
    {
        Assert.That(Normalized(Field(CustomFieldKind.MultiSelect, "Oak"), "[]"), Is.Null);
    }

    [Test]
    public void MultiSelect_WithAnUnknownOption_IsRejected()
    {
        var field = Field(CustomFieldKind.MultiSelect, "Oak");

        Assert.Throws<DomainValidationException>(() => field.Normalize(Json($"[\"{field.Options[0].Id}\", \"{Guid.NewGuid()}\"]")));
    }

    [Test]
    public void ReviseOptions_RenamesReordersAddsAndRemoves()
    {
        var field = Field(CustomFieldKind.SingleSelect, "Oak", "Pine", "Birch");
        var (oak, birch) = (field.Options[0].Id, field.Options[2].Id);

        field.ReviseOptions([new(birch, "Silver birch"), new(null, "Ash"), new(oak, "Oak")]);

        Assert.Multiple(() =>
        {
            Assert.That(field.Options.Select(o => o.Name), Is.EqualTo(new[] { "Silver birch", "Ash", "Oak" }));
            Assert.That(field.Options[0].Id, Is.EqualTo(birch));
            Assert.That(field.Options[2].Id, Is.EqualTo(oak));
        });
    }

    [Test]
    public void ReviseOptions_NamingAnOptionTheFieldDoesNotHave_IsRejected()
    {
        var field = Field(CustomFieldKind.SingleSelect, "Oak");

        Assert.Throws<DomainValidationException>(() => field.ReviseOptions([new(Guid.NewGuid(), "Pine")]));
    }

    [Test]
    public void Prune_DropsOptionsTheFieldNoLongerHas()
    {
        var field = Field(CustomFieldKind.MultiSelect, "Oak", "Pine");
        var (oak, pine) = (field.Options[0].Id, field.Options[1].Id);
        var stored = field.Normalize(Json($"[\"{oak}\", \"{pine}\"]"))!.Value;

        field.ReviseOptions([new(pine, "Pine")]);

        Assert.That(field.Prune(stored)?.GetRawText(), Is.EqualTo($"[\"{pine}\"]"));
    }

    [Test]
    public void Prune_OfASingleSelectWhoseOptionWasRemoved_ClearsIt()
    {
        var field = Field(CustomFieldKind.SingleSelect, "Oak", "Pine");
        var stored = field.Normalize(Json($"\"{field.Options[0].Id}\""))!.Value;

        field.ReviseOptions([new(field.Options[1].Id, "Pine")]);

        Assert.That(field.Prune(stored), Is.Null);
    }
}
