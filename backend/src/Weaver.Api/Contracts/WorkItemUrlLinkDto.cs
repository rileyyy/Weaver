using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

public record WorkItemUrlLinkDto(Guid Id, string Url, string? Label)
{
    public static WorkItemUrlLinkDto FromView(WorkItemUrlLinkView link) => new(link.Id, link.Url, link.Label);
}
