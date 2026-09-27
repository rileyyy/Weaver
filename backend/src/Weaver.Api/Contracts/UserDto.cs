using Weaver.Domain;

namespace Weaver.Api.Contracts;

public record UserDto(Guid Id, string Username, UserKind Kind)
{
    public static UserDto FromEntity(User user) => new(user.Id, user.Username, user.Kind);
}
