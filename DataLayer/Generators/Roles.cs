using DataLayer.Entities;

namespace DataLayer.Generators
{
    public class Roles : IGenerator<Role>
    {
        public static IEnumerable<Role> Generate()
        {
            IEnumerable<Role> roles =
            [
                // these become default expectation of access, rather than static settings
                new Role{ Name = DefaultRoles.Admin.ToString(), Description = "General administrator, full control" },
                new Role{ Name = DefaultRoles.Client.ToString(), Description = "General client, like a doctor or nurse" },
                new Role{ Name = DefaultRoles.Tech.ToString(), Description = "General technician, device certified" },
                new Role{ Name = DefaultRoles.Guest.ToString(), Description = "General guest, for emergent use" }
            ];
            return roles;
        }
    }
}
