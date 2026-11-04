namespace Weaver.Domain;

/// <summary>
/// What a custom field holds (ADR-0002). Fixed when the field is created: changing it would
/// leave every stored value in the wrong shape.
/// </summary>
public enum CustomFieldKind
{
    Text,
    Markdown,
    Number,
    Currency,
    Date,
    Checkbox,
    SingleSelect,
    MultiSelect,
    User,
    Url,
}
