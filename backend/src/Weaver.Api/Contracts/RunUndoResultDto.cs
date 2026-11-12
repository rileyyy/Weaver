using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>Every change the undone run made, in the order it made them, each with what the undo did.</summary>
public record RunUndoResultDto(IReadOnlyList<RunUndoChangeDto> Changes)
{
    public static RunUndoResultDto FromResult(RunUndoResult result) =>
        new(result.Changes.Select(RunUndoChangeDto.FromChange).ToList());
}
