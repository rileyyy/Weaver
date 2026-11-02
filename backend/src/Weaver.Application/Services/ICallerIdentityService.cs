namespace Weaver.Application.Services;

/// <summary>Answers "who am I signed in as?" for the current caller, e.g. an agent checking its key.</summary>
public interface ICallerIdentityService
{
    Task<CallerIdentity> DescribeAsync(CancellationToken ct = default);
}
