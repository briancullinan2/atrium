using DataLayer.Entities;

namespace DataLayer.Generators
{
    public class Roles : IGenerator<Role>
    {
        public static IEnumerable<Role> Generate()
        {
            IEnumerable<Role> roles =
            [
                new Role{ Name = "Admin", Description = "General administrator, full control" },
                new Role{ Name = "Client", Description = "General client, like a doctor or nurse" },
                new Role{ Name = "Tech", Description = "General technician, device certified" },
                new Role{ Name = "Guest", Description = "General guest, for emergent use" }
            ];
            return roles;
        }
    }
}
