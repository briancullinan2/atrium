namespace DataShared.ForeignEntity;

public interface IGenerator<T> where T : IEntity
{
    static abstract IEnumerable<T> Generate();
}
