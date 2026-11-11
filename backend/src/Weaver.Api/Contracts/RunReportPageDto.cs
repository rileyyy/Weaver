using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

/// <summary>One page of runs, newest first. <see cref="NextOffset"/> is null on the last page.</summary>
public record RunReportPageDto(IReadOnlyList<RunReportDto> Runs, int? NextOffset)
{
    public static RunReportPageDto FromPage(RunReportPage page) => new(
        page.Runs.Select(RunReportDto.FromReport).ToList(),
        page.NextOffset);
}
