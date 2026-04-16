

namespace UserData.Generators;

public class Users : IGenerator<User>
{

    // TODO: i did this in studysauce2, i basically allowed guests to "impersonate" a
    //   specific guest account with turned off save controls using the same built
    //   in symphony impersonate system that comes with user management.
    public static IEnumerable<User> Generate()
    {
        return [
            new User { FirstName = "System", LastName = "Admin", Username = nameof(DefaultRoles.Admin), Password = "Password123!", MiddleInitial = "A", Roles = [new() { Name = nameof(DefaultRoles.Admin) }] },
            new User { FirstName = "Guest", LastName = "Account", Username = nameof(DefaultRoles.Guest), Password = "GuestPassword1!", MiddleInitial = "G", Roles = [new() { Name = nameof(DefaultRoles.Guest) }] },
            new User { FirstName = "Technical", LastName = "Support", Username = nameof(DefaultRoles.Tech), Password = "TechSupport1!", MiddleInitial = "T", Roles = [new() { Name = nameof(DefaultRoles.Tech) }] },
            new User { FirstName = "Standard", LastName = "Client", Username = nameof(DefaultRoles.Client), Password = "ClientUser1!", MiddleInitial = "C", Roles = [new() { Name = nameof(DefaultRoles.Client) }] }
        ];
    }
}
