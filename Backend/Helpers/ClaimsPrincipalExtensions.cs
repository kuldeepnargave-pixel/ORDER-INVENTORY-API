using System.Security.Claims;
using Backend.Helpers;

namespace Backend.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var id))
        {
            throw new UnauthorizedException("The access token does not contain a valid user id.");
        }
        return id;
    }
}
