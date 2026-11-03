using Weaver.Domain;

namespace Weaver.Application.Services;

public record ResolvedQuickPick(QuickPick Pick, DateRange Dates);
