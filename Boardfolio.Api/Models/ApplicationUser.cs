using Microsoft.AspNetCore.Identity;

namespace Boardfolio.Api.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.NewGuid();
}
