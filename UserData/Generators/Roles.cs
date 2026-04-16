

namespace UserData.Generators;

public class Roles : IGenerator<Role>
{
    public static IEnumerable<Role> Generate()
    {
        IEnumerable<Role> roles =
        [
            // these become default expectation of access, rather than static settings
            new Role{ Name = nameof(DefaultRoles.Admin), Description = "General administrator, full control" },
            new Role{ Name = nameof(DefaultRoles.Client), Description = "General client, like a doctor or nurse" },
            new Role{ Name = nameof(DefaultRoles.Tech), Description = "General technician, device certified" },
            new Role{ Name = nameof(DefaultRoles.Guest), Description = "General guest, for emergent use" }
        ];
        return roles;
    }
}
