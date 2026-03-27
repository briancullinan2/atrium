using DataLayer.Entities;

namespace DataLayer.Generators
{
    public class Users : IGenerator<User>
    {

        // TODO: i did this in studysauce2, i basically allowed guests to "impersonate" a
        //   specific guest account with turned off save controls using the same built
        //   in symphony impersonate system that comes with user management.
        public static IEnumerable<User> Generate()
        {
            return [
                new User { FirstName = "System", LastName = "Admin", Username = DefaultRoles.Admin.ToString(), Password = "Password123!", MiddleInitial = "A", Roles = [new() { Name = DefaultRoles.Admin.ToString() }] },
                new User { FirstName = "Guest", LastName = "Account", Username = DefaultRoles.Guest.ToString(), Password = "GuestPassword1!", MiddleInitial = "G", Roles = [new() { Name = DefaultRoles.Guest.ToString() }] },
                new User { FirstName = "Technical", LastName = "Support", Username = DefaultRoles.Tech.ToString(), Password = "TechSupport1!", MiddleInitial = "T", Roles = [new() { Name = DefaultRoles.Tech.ToString() }] },
                new User { FirstName = "Standard", LastName = "Client", Username = DefaultRoles.Client.ToString(), Password = "ClientUser1!", MiddleInitial = "C", Roles = [new() { Name = DefaultRoles.Client.ToString() }] }
            ];
        }
    }
}
