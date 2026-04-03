using DataLayer.Entities;

namespace DataLayer.Generators
{
    public interface IGenerator<T> where T : IEntity
    {
        static abstract IEnumerable<T> Generate();
    }
}
