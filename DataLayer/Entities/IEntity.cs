using DataLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Runtime.CompilerServices;
using DataLayer.Utilities.Extensions;

namespace DataLayer.Entities
{

    public interface IEntity
    {
        //abstract internal static IEntity Create(IEntity target);
        //abstract internal static IEntity Wrap(IEntity target);
        //Task<IEntity> Update(IEntity? entity = null);
        //Task<TEntity> Update<TEntity>(TEntity? entity = null) where TEntity : Entity<TEntity>, IEntity<TEntity>, IEntity;
        //Task<IEntity> Save();
        int? CanonicalFingerprint { get; set; }

    }

    public interface IEntity<T> : IEntity where T : Entity<T>, IEntity<T>
    {
        Task<TEntity> Update<TEntity>(TEntity? entity = null) where TEntity : Entity<TEntity>, IEntity<TEntity>, IEntity<T>, IEntity;
        Task<T> Save();
    }


    public class Entity<T> : IEntity<T> where T : Entity<T>, IEntity<T>, IEntity
    {

        public static EntityMetadata<T> Metadata => new();

        public int? CanonicalFingerprint { get; set; } = null;

        /*
        [NotMapped]
        public IEnumerable<PropertyInfo> DatabaseProperties { get => ListDatabaseProperties(typeof(T)); }
        [NotMapped]
        public IEnumerable<PropertyInfo> InterestingProperties { get => ListInterestingProperties(typeof(T)); }
        */

        private static IEnumerable<PropertyInfo> ListDatabaseProperties(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    p.GetGetMethod()?.IsVirtual != true
                    && !Attribute.IsDefined(p, typeof(KeyAttribute))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute)));

            return properties;
        }

        private static IEnumerable<PropertyInfo> ListInterestingProperties(Type type)
        {
            //var ignoreList = new List<string>(ignore);
            var foreignKeys = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => Attribute.GetCustomAttribute(p, typeof(ForeignKeyAttribute))?.TypeId);

            // Get properties that are NOT virtual (Nav properties) and NOT marked [NotMapped]
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    !foreignKeys.Contains(p.Name) // TODO: skip all FK IDs because they might not match on server
                    && p.GetGetMethod()?.IsVirtual != true
                    && !Attribute.IsDefined(p, typeof(KeyAttribute))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    && !Attribute.IsDefined(p, typeof(ForeignKeyAttribute)) // comparing id is enough
                    && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                            //&& !ignoreList.Contains(p.Name)
                            );

            return properties;
        }



        public override bool Equals(object? obj)
        {
            if (this == null || obj == null) return this == obj;

            var type = typeof(T);
            var properties = ListInterestingProperties(type);

            foreach (var prop in properties)
            {
                var selfValue = prop.GetValue(this, null);
                var toValue = prop.GetValue(obj, null);

                if (selfValue != toValue && (selfValue == null || !selfValue.Equals(toValue)))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            if (CanonicalFingerprint != null)
                return (int)CanonicalFingerprint;

            var type = typeof(T);
            // Sort by name so the hash is deterministic across different runs/platforms
            var properties = ListInterestingProperties(type).OrderBy(p => p.Name);

            uint hash = 2166136261;
            uint prime = 16777619;

            foreach (var prop in properties)
            {
                object? val = prop.GetValue(this, null);
                if (val == null) continue;

                int propertyHash;

                // If it's a string, we treat it semantically
                if (val is string str)
                {
                    // If it's the specific fingerprint property, it's already "clean"
                    propertyHash = (int)FingerPrint.GetSemanticFingerprint(str);
                }
                else
                {
                    // For non-strings (DateTime, int, etc.), use standard hash
                    propertyHash = val.GetHashCode();
                }

                unchecked
                {
                    hash ^= (uint)propertyHash;
                    hash *= prime;
                }
            }

            return (int)hash;
        }

        public async Task<T> Save()
        {
            return (T)(await IEntityExtensions.Save<T>(this as T));
        }

        /*

        async Task<IEntity> IEntity.Save()
        {
            return await IEntityExtensions.Save(this);
        }
        */

        /*
        async Task<IEntity> IEntity.Update(IEntity? entity)
        {
            return await IEntityExtensions.Update(entity);
        }
        */

        public async Task<TEntity> Update<TEntity>(TEntity? entity = null) where TEntity : Entity<TEntity>, IEntity<TEntity>, IEntity<T>, IEntity
        {
            entity ??= this as TEntity;
            return await IEntityExtensions.Update(entity!);
        }

        //async Task<TEntity> IEntity.Update<TEntity>(TEntity? entity) where TEntity : class
        //{
        //    return await IEntityExtensions.Update(entity!);
        //}
    }

}
