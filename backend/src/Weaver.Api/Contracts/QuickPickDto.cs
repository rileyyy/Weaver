using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record QuickPickDto(QuickPick Pick, DateOnly StartDate, DateOnly EndDate)
{
    public static QuickPickDto FromResolved(ResolvedQuickPick resolved) =>
        new(resolved.Pick, resolved.Dates.Start, resolved.Dates.End);
}
