
namespace Extensions.ForeignEntity
{
    public static partial class IEntityExtensions
    {


        public static List<PropertyInfo> Database(this Type type, bool includePredicate = false)
        {
            var primaryKey = type.GetCustomAttribute<PrimaryKeyAttribute>()?.PropertyNames;
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    includePredicate
                    || (primaryKey?.Contains(p.Name) != true
                    && !Attribute.IsDefined(p, typeof(KeyAttribute)))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute)))
                .OrderBy(p => p.Name)
                .ToList();

            return properties;
        }



        public static Dictionary<string, List<PropertyInfo>> Indexes(this Type type, bool includePrimary = true)
        {
            var indexes = type.GetCustomAttributes<IndexAttribute>()
                .ToDictionary<IndexAttribute, string, List<PropertyInfo>>(
                    i => i.Name ?? string.Empty,
                    i => [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => i.PropertyNames.Contains(p.Name))]);
            if (includePrimary
                && type.GetCustomAttribute<PrimaryKeyAttribute>() is PrimaryKeyAttribute attr)
            {
                var primaryKey = string.Join("", attr.PropertyNames);
                var primaryProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => primaryKey.Contains(p.Name))
                    .ToList();
                indexes[primaryKey] = primaryProperties;
            }
            return indexes;
        }


        public static List<PropertyInfo> Interesting(this Type type)
        {
            var foreignKeys = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => Attribute.GetCustomAttribute(p, typeof(ForeignKeyAttribute))?.TypeId);

            // Get properties that are NOT virtual (Nav properties) and NOT marked [NotMapped]
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    !foreignKeys.Contains(p.Name) // TODO: skip all FK IDs because they might not match on server
                                                  //&& p.GetGetMethod()?.IsVirtual != true
                    && !Attribute.IsDefined(p, typeof(KeyAttribute))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    && !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute))
                    && !Attribute.IsDefined(p, typeof(ForeignKeyAttribute)) // comparing id is enough
                    && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType))
                .OrderBy(p => p.Name)
                .ToList();

            return properties;
        }

        public static List<PropertyInfo> Display(this Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    (Attribute.IsDefined(p, typeof(CategoryAttribute))
                    || Attribute.IsDefined(p, typeof(DisplayAttribute)))
                    && (string.Equals(p.GetCustomAttribute<CategoryAttribute>()?.Category, "Display")
                    || string.Equals(p.GetCustomAttribute<DisplayAttribute>()?.GroupName, "Display"))
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    && !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute))

                    )
                .OrderBy(p => p.Name)
                .ToList();

            return properties;
        }



        public static List<(string Name, Type EntityType)> Schemas(DbContext context)
        {
            return context.GetType().Schemas();
        }


        public static List<(string Name, Type EntityType)> Schemas(this Type contextType)
        {
            if (contextType.Extends(typeof(ITranslationContext)))
                return [.. (contextType.GetProperties(nameof(ITranslationContext.EntityTypes))
                    .FirstOrDefault()?.GetValue(null) as List<Type>)
                    ?.Select(p => (
                        Name: p.Table() ?? p.Name,
                        EntityType: p
                    )) ?? []];


            List<(string Name, Type EntityType)> sets = [.. contextType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .Select(p => (
                    Name: p.PropertyType.GetGenericArguments()[0].Table() ?? p.Name,
                    EntityType: p.PropertyType.GetGenericArguments()[0]
                ))];

            if (sets.Count != 0) return sets;

            if (!contextType.Extends(typeof(ITranslationContext)))
                throw new InvalidOperationException("Not sure what to do here, type is not a TranslationContext");

            throw new InvalidOperationException("Not sure what to do here, how to get a list of DbSets<>");
        }

    }
}
