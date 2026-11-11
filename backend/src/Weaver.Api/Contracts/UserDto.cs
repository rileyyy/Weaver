using Weaver.Domain;

namespace Weaver.Api.Contracts;

/// <summary><see cref="Handle"/> is what the comment box autocompletes after an <c>@</c>.</summary>
public record UserDto(Guid Id, string Username, UserKind Kind, bool IsAdmin, string? Handle = null)
{
    public static UserDto FromEntity(User user) => new(user.Id, user.Username, user.Kind, user.IsAdmin, user.Handle);
}
