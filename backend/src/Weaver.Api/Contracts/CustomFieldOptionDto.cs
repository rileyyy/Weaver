using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary>A select field's choice; values store its id.</summary>
public record CustomFieldOptionDto(Guid Id, string Name)
{
    public static CustomFieldOptionDto FromOption(CustomFieldOption option) => new(option.Id, option.Name);
}
