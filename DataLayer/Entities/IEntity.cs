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
using System.Text.Json.Serialization;

namespace DataLayer.Entities
{

    public interface IEntity : IDisposable
    {
        //abstract internal static IEntity Create(IEntity target);
        //abstract internal static IEntity Wrap(IEntity target);
        //Task<IEntity> Update(IEntity? entity = null);
        //Task<TEntity> Update<TEntity>(TEntity? entity = null) where TEntity : Entity<TEntity>, IEntity<TEntity>, IEntity;
        //Task<IEntity> Save();
        int? CanonicalFingerprint { get; set; }
        internal IQueryManager? QueryManager { get; set; }
        internal Type? ContextType { get; set; }



    }

    public interface IEntity<T> : IEntity where T : Entity<T>, IEntity<T>
    {

        static abstract List<PropertyInfo> Predicate { get; }
        static abstract List<PropertyInfo> Display { get; }
        static abstract List<PropertyInfo> Database { get; }
        static abstract List<PropertyInfo> Interesting { get; }
        static abstract EntityMetadata<T> Metadata { get; }

        Task<TEntity> Update<TEntity>(TEntity? entity = null) where TEntity : Entity<TEntity>, IEntity<TEntity>, IEntity<T>, IEntity;
        //Task<T> Save(IServiceProvider service);
        Task<T> Save(IQueryManager? query = null);
    }


    public class Entity<T> : IEntity<T> where T : Entity<T>, IEntity<T>, IEntity, IDisposable
    {
        [JsonIgnore]
        [NotMapped]
        public IQueryManager? QueryManager { get; set; } = null;
        [NotMapped]
        [JsonIgnore]
        public Type? ContextType { get; set; } = null;
        [NotMapped]

        public static EntityMetadata<T> Metadata { get; } = new();

        public int? CanonicalFingerprint { get; set; } = null;

        //[Obsolete("Use context.Create<Role>() to ensure the RemoteQuery bridge is initialized.", error: true)] 
        protected Entity() { }

        public void Dispose()
        {
            QueryManager = null;
            ContextType = null;
            GC.SuppressFinalize(this);
        }

        [NotMapped]
        public static List<PropertyInfo> Database { get => IEntityExtensions.ListDatabase(typeof(T)); }
        [NotMapped]
        public static List<PropertyInfo> Interesting { get => IEntityExtensions.ListInteresting(typeof(T)); }
        [NotMapped]
        public static List<PropertyInfo> Display { get => IEntityExtensions.ListDisplay(typeof(T)); }
        [NotMapped]
        public static List<PropertyInfo> Predicate { get => IEntityExtensions.ListPredicate(typeof(T)); }
        [NotMapped]
        public static Dictionary<string, List<PropertyInfo>> Indexes { get => IEntityExtensions.ListIndexes(typeof(T)); }


        public override bool Equals(object? obj)
        {
            if (this == null || obj == null) return this == obj;

            foreach (var prop in Interesting)
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

            uint hash = 2166136261;
            uint prime = 16777619;

            foreach (var prop in Database)
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

        //public async Task<T> Save(IServiceProvider? service)
        //{
        //    return (T)(await IEntityExtensions.Save(this as T, service ?? Utilities.QueryManager.Service));
        //}

        public async Task<T> Save(IQueryManager? query = null)
        {
            return (T)(await IEntityExtensions.Save(this as T, query ?? QueryManager));
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


        public async Task<T> Update(IQueryManager query)
        {
            return await IEntityExtensions.Update<T>((T)this, query);
        }

        public async Task<TEntity?> Update<TEntity>(TEntity? entity = null, IQueryManager? query = null) where TEntity : Entity<TEntity>, IEntity<TEntity>, IEntity<T>, IEntity
        {
            if (entity == null && this is TEntity that)
            {
                return await IEntityExtensions.Update<TEntity>(that, query);
            }
            else if (entity != null)
            {
                return await IEntityExtensions.Update<TEntity>(entity, query);
            }
            else
                return default;
        }

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
