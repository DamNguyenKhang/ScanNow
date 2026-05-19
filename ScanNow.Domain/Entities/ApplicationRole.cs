using Microsoft.AspNetCore.Identity;

namespace ScanNow.Domain.Entities
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public ApplicationRole()
        {
        }

        public ApplicationRole(string roleName)
            : base(roleName)
        {
        }

        public string? Description { get; set; }
    }
}
