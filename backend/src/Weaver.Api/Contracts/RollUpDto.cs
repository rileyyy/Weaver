using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>
/// The sum of one roll-up field over an item's live descendants, not counting the item's own
/// value (which is in the item's <c>fields</c>, when its Type has the field). A currency total
/// is in the field's one currency.
/// </summary>
public record RollUpDto(Guid FieldId, string FieldName, Guid TypeId, CustomFieldKind Kind, decimal Total)
{
    public static RollUpDto FromTotal(RollUpTotal total) =>
        new(total.Field.Id, total.Field.Name, total.Field.TypeId, total.Field.Kind, total.Total);
}
