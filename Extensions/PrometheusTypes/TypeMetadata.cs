
namespace Extensions.PrometheusTypes
{
    public static partial class TypeExtensions
    {

        private static readonly Dictionary<Type, EntityMetadata> _metadataCache = [];




        [Obsolete("This probably isn't what you want, Metadata of a Metadata?")]
        public static EntityMetadata Metadata(this EntityMetadata any)
        {
            return any;
        }


        public static EntityMetadata Metadata(this object any)
        {
            return any.GetType().Metadata();
        }

        public static EntityMetadata Metadata(this Type any)
        {
            if (_metadataCache.TryGetValue(any, out var meta))
                return meta;
            var newMeta = new EntityMetadata(any);
            _metadataCache.TryAdd(any, newMeta);
            return newMeta;
        }



    }
}
