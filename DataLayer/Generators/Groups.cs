using DataLayer.Entities;

namespace DataLayer.Generators
{
    public class Groups : IGenerator<Entities.Group>
    {
        public static IEnumerable<Group> Generate()
        {
            return [
                new Group() { Name = "Real Estate" },
                new Group() { Name = "Immigration" },
                new Group() { Name = "Atrium" },
            ];
        }
    }
}
