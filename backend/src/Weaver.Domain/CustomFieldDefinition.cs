using System.Globalization;
using System.Text.Json;
using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A field a <see cref="WorkItemType"/> defines (ADR-0002). Its name and order are unique among
/// its Type's fields. It decides what a valid value is: <see cref="Normalize"/> turns what a
/// caller sent into the one stored shape, so equal values compare equal.
/// </summary>
public class CustomFieldDefinition : LookupValue
{
    public const int TextMaxLength = 500;
    public const int MarkdownMaxLength = 20_000;
    public const int UrlMaxLength = 2_000;
    public const int CurrencyDecimals = 2;

    public const int MaxOptions = 100;

    private const string DateFormat = "yyyy-MM-dd";

    private List<CustomFieldOption> _options = [];

    /// <summary>For EF Core.</summary>
    private CustomFieldDefinition()
    {
    }

    private CustomFieldDefinition(Guid typeId, string name, CustomFieldKind kind, int order) : base(name, order)
    {
        TypeId = typeId;
        Kind = kind;
    }

    public Guid TypeId { get; private set; }

    public CustomFieldKind Kind { get; private set; }

    /// <summary>The choices of a select field, in display order; empty for every other kind.</summary>
    public IReadOnlyList<CustomFieldOption> Options
    {
        get => _options;
        private set => _options = [.. value];
    }

    public bool IsSelect => Kind is CustomFieldKind.SingleSelect or CustomFieldKind.MultiSelect;

    /// <summary>The name its changes go by in history and in a write's changed fields.</summary>
    public string ChangeName => WorkItemField.CustomField(Id);

    public static CustomFieldDefinition Create(
        Guid typeId,
        string name,
        CustomFieldKind kind,
        int order,
        IEnumerable<string>? optionNames = null)
    {
        var field = new CustomFieldDefinition(typeId, name, kind, order);
        field.ReviseOptions((optionNames ?? []).Select(n => new CustomFieldOptionRevision(null, n)));
        return field;
    }

    /// <summary>
    /// Replaces the option list: <paramref name="options"/> is the whole new list, in order.
    /// An option left out is removed; items that chose it lose that choice (see <see cref="Prune"/>).
    /// </summary>
    public void ReviseOptions(IEnumerable<CustomFieldOptionRevision> options)
    {
        var revised = options.ToList();
        if (revised.Count > 0 && !IsSelect)
        {
            throw new DomainValidationException($"Only a select field has options; {Name} is a {Kind} field.");
        }

        if (revised.Count > MaxOptions)
        {
            throw new DomainValidationException($"A field can have at most {MaxOptions} options.");
        }

        var existing = _options.ToDictionary(o => o.Id);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var next = new List<CustomFieldOption>();
        foreach (var option in revised)
        {
            TextValidation.RequireText(option.Name, "Option name", NameMaxLength);
            var name = option.Name.Trim();
            if (!names.Add(name))
            {
                throw new DomainValidationException($"{Name} already has an option named '{name}'.");
            }

            if (option.Id is { } id && !existing.ContainsKey(id))
            {
                throw new DomainValidationException($"{Name} has no option {id}.");
            }

            next.Add(new CustomFieldOption(option.Id ?? Guid.NewGuid(), name));
        }

        _options = next;
    }

    /// <summary>
    /// A value stored before the options last changed, without the options since removed, or
    /// <c>null</c> when none of its choices are left. Other kinds come back unchanged.
    /// </summary>
    public JsonElement? Prune(JsonElement stored) => Kind switch
    {
        CustomFieldKind.SingleSelect => HasOption(stored.GetString()) ? stored : null,
        CustomFieldKind.MultiSelect => SelectedInOptionOrder(
            stored.EnumerateArray().Select(e => e.GetString()).Where(HasOption).Select(id => Guid.Parse(id!))),
        _ => stored,
    };

    /// <summary>
    /// The value as stored, or <c>null</c> when <paramref name="value"/> clears the field (JSON
    /// null, or a blank text).
    /// </summary>
    /// <exception cref="DomainValidationException">The value doesn't fit this field.</exception>
    public JsonElement? Normalize(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return Kind switch
        {
            CustomFieldKind.Text => NormalizeText(value, TextMaxLength),
            CustomFieldKind.Markdown => NormalizeText(value, MarkdownMaxLength),
            CustomFieldKind.Number => NormalizeNumber(value, maxDecimals: null),
            CustomFieldKind.Currency => NormalizeNumber(value, CurrencyDecimals),
            CustomFieldKind.Date => NormalizeDate(value),
            CustomFieldKind.Checkbox => NormalizeCheckbox(value),
            CustomFieldKind.Url => NormalizeUrl(value),
            CustomFieldKind.User => JsonSerializer.SerializeToElement(RequireId(value, "a user id")),
            CustomFieldKind.SingleSelect => NormalizeSingleSelect(value),
            CustomFieldKind.MultiSelect => NormalizeMultiSelect(value),
            _ => throw new DomainValidationException($"{Name} can't hold a value of kind {Kind}."),
        };
    }

    private JsonElement? NormalizeSingleSelect(JsonElement value)
    {
        var option = RequireId(value, "one of its option ids");
        RequireOption(option);
        return JsonSerializer.SerializeToElement(option);
    }

    private JsonElement? NormalizeMultiSelect(JsonElement value)
    {
        var chosen = RequireKind(value, JsonValueKind.Array, "a list of its option ids")
            .EnumerateArray()
            .Select(e => RequireId(e, "a list of its option ids"))
            .ToList();
        chosen.ForEach(RequireOption);
        return SelectedInOptionOrder(chosen);
    }

    /// <summary>Option order, so choosing the same options in another order isn't a change.</summary>
    private JsonElement? SelectedInOptionOrder(IEnumerable<Guid> chosen)
    {
        var set = chosen.ToHashSet();
        var ordered = _options.Where(o => set.Contains(o.Id)).Select(o => o.Id).ToList();
        return ordered.Count == 0 ? null : JsonSerializer.SerializeToElement(ordered);
    }

    private bool HasOption(string? id) => Guid.TryParse(id, out var guid) && _options.Any(o => o.Id == guid);

    private void RequireOption(Guid id)
    {
        if (_options.All(o => o.Id != id))
        {
            throw new DomainValidationException($"{Name} has no option {id}.");
        }
    }

    private Guid RequireId(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var id))
        {
            throw new DomainValidationException($"{Name} takes {description}.");
        }

        return id;
    }

    private JsonElement? NormalizeNumber(JsonElement value, int? maxDecimals)
    {
        if (RequireKind(value, JsonValueKind.Number, "a number").TryGetDecimal(out var number) is false)
        {
            throw new DomainValidationException($"{Name} is out of range.");
        }

        // Trailing zeros are dropped so 1.50 and 1.5 are the same value, not a change.
        var normalized = decimal.Parse(number.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (maxDecimals is { } max && normalized.Scale > max)
        {
            throw new DomainValidationException($"{Name} takes an amount with at most {max} decimals.");
        }

        return JsonSerializer.SerializeToElement(normalized);
    }

    private JsonElement? NormalizeDate(JsonElement value)
    {
        var text = RequireKind(value, JsonValueKind.String, "a date (yyyy-MM-dd)").GetString();
        if (!DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new DomainValidationException($"{Name} takes a date (yyyy-MM-dd).");
        }

        return JsonSerializer.SerializeToElement(date.ToString(DateFormat, CultureInfo.InvariantCulture));
    }

    private JsonElement? NormalizeCheckbox(JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new DomainValidationException($"{Name} takes true or false.");
        }

        return value.Clone();
    }

    /// <summary>Only web addresses, so a stored link can't run script when the app opens it.</summary>
    private JsonElement? NormalizeUrl(JsonElement value)
    {
        if (NormalizeText(value, UrlMaxLength) is not { } text)
        {
            return null;
        }

        if (!Uri.TryCreate(text.GetString(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainValidationException($"{Name} takes an http or https address.");
        }

        return text;
    }

    private JsonElement? NormalizeText(JsonElement value, int maxLength)
    {
        var text = RequireKind(value, JsonValueKind.String, "text").GetString()!.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        TextValidation.RequireMaxLength(text, Name, maxLength);
        return JsonSerializer.SerializeToElement(text);
    }

    private JsonElement RequireKind(JsonElement value, JsonValueKind expected, string description)
    {
        if (value.ValueKind != expected)
        {
            throw new DomainValidationException($"{Name} takes {description}.");
        }

        return value;
    }
}
