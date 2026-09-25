using TeleMed.Domain.Entities;

namespace TeleMed.Application.Abstractions;

public interface ITokenIssuer
{
    TimeSpan AccessTokenLifetime { get; }
    TimeSpan RefreshTokenLifetime { get; }
    string IssueAccessToken(User user);
}
