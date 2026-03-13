using DataLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DataLayer.Entities
{

    public interface IEntity
    {
        //abstract internal static IEntity Create(IEntity target);
        //abstract internal static IEntity Wrap(IEntity target);
    }

    public interface IEntity<T> : IEntity where T : class, IEntity<T>
    {
        int? CanonicalFingerprint { get; set; }
    }

    abstract public class Entity : DispatchProxy, INotifyPropertyChanged
    {
        protected abstract override object? Invoke(MethodInfo? targetMethod, object?[]? args);
        public abstract event PropertyChangedEventHandler? PropertyChanged;

        public static ProxyEntity<T> Wrap<T>(IEntity<T> target, IServiceProvider service, Type? context = null) where T : class, IEntity<T>
        {
            return Wrap((IEntity)target, service, context) as ProxyEntity<T>;
        }

        public static ProxyEntity<T> Wrap<T>(IEntity<T> target, IServiceProvider service) where T : class, IEntity<T>
        {
            return Wrap((IEntity)target, service) as ProxyEntity<T>;
        }

        public static IEntity Wrap(IEntity target, IServiceProvider? service = null, Type? context = null)
        {
            // 1. Create the specific interface type: IEntity<MyEntity>
            Type interfaceType = typeof(IEntity<>).MakeGenericType(target.GetType());

            // 2. Create the specific proxy implementation type (assuming your class is EntityProxy<T>)
            // DispatchProxy requires a class that inherits from DispatchProxy
            Type proxyType = typeof(ProxyEntity<>).MakeGenericType(target.GetType());

            // 3. Since DispatchProxy.Create<T, TProxy> is generic, invoke it via reflection
            var createMethod = typeof(DispatchProxy)
                .GetMethod(nameof(DispatchProxy.Create), 2, [])
                .MakeGenericMethod(interfaceType, proxyType);

            object proxy = createMethod.Invoke(null, null);

            // 4. Set your fields (make sure they are public or use GetField with BindingFlags)
            var targetField = proxy.GetType().GetField("_target", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            targetField?.SetValue(proxy, target);

            if (service != null)
            {
                var serviceField = proxy.GetType().GetField("_service", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                serviceField?.SetValue(proxy, service);
            }

            if (context != null)
            {
                var contextField = proxy.GetType().GetField("_context", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                contextField?.SetValue(proxy, context);
            }

            return proxy as IEntity;
        }

    }

    public class Entity<T> : IEntity<T> where T : class, IEntity<T>
    {
        public ProxyEntity<T> Wrap(IServiceProvider? service = null, bool persist = true)
        {
            return Entity.Wrap(this, service, persist ? typeof(IDbContextFactory<DataLayer.PersistentStorage>) : typeof(IDbContextFactory<DataLayer.EphemeralStorage>));
        }

        public ProxyEntity<T> Wrap(IServiceProvider service, Type context)
        {
            return Entity.Wrap(this, service, context);
        }


        public int? CanonicalFingerprint { get; set; } = null;


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

    }

    public class ProxyEntity<T> : Entity, IDisposable, INotifyPropertyChanged where T : class, IEntity<T>
    {
        public override event PropertyChangedEventHandler? PropertyChanged;

        public ProxyEntity() : base()
        {
            // observables don't like this because of invoke recursion bla bla bla, damn gemini is chatty
            // _target = this;
        }

        public T _target = default!;
        public IServiceProvider? _service = default!;
        public Type? _context = default!;
        public IServiceScope? _scope = default;

        public T Save(bool? recurse = false)
        {
            return Utilities.Extensions.IEntityExtensions.Save(this, recurse);
        }

        public T Attach(ProxyEntity<T> entity, bool? recurse = false)
        {
            return Utilities.Extensions.IEntityExtensions.Refetch(entity, recurse);
        }

        public override bool Equals(object? obj)
        {
            return _target.Equals((obj as ProxyEntity<T>)?._target);
        }

        public override int GetHashCode()
        {
            return _target.GetHashCode();
        }



        public TExtra Attach<TExtra>(Entity<TExtra>? entity, bool? recurse = false) where TExtra : class, IEntity<TExtra>
        {
            if (entity == null)
            {
                return default!;
            }
            var proxy = entity.Wrap(_service, _context);
            proxy._scope = this._scope;
            return Utilities.Extensions.IEntityExtensions.Refetch(proxy, recurse);
        }

        public T Refetch(bool? recurse = false)
        {
            return Utilities.Extensions.IEntityExtensions.Refetch(this, recurse);
        }

        // 1. You create a static helper to wrap the real object
        public static T Create(T target, IServiceProvider service)
        {
            // This calls the internal EF/WPF magic to create the proxy
            object proxy = DispatchProxy.Create<T, ProxyEntity<T>>();

            // This sets the _target field so Invoke can use it later
            ((ProxyEntity<T>)proxy)._target = target;
            ((ProxyEntity<T>)proxy)._service = service;

            return (T)proxy;
        }

        //static T IEntity<T>.Wrap(T target, IServiceProvider service)
        //{
        //    return (T)Wrap((IEntity)target, service);
        //}

        protected void SetValue<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;

            // "Looking for null or subs": 
            // If PropertyChanged is null (no WPF binding), nothing happens.
            // If it has subs, it notifies the UI.
            PropertyChanged?.Invoke(_target, new PropertyChangedEventArgs(propertyName));

            // Lead back to your static TranslationContext
            OnEntityChanged(propertyName);
        }

        private void OnEntityChanged(string prop)
        {
            // Add your logic to track "dirty" states for your Cloud/Disk swap
            Console.WriteLine($"Property {prop} changed. Ready for sync.");
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var result = targetMethod?.Invoke(_target, args);

            if (targetMethod?.Name.StartsWith("set_") == true)
            {
                var propName = targetMethod.Name.Substring(4);
                // Trigger your WPF logic here
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
                OnEntityChanged(propName);
            }
            if (targetMethod?.Name.StartsWith("get_") == true && result == null)
            {
                var prop = _target.GetType().GetProperty(targetMethod.Name.Substring(4));
                using var scope = _service.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TranslationContext>();

                if (typeof(IEntity<>).IsAssignableFrom(prop.PropertyType))
                {
                    // 1. Force EF to hit the DB/Disk/Cloud
                    context.Entry(_target).Reference(prop.Name).Load();

                    // 2. Retrieve the now-populated value
                    var rawValue = prop.GetValue(_target) as IEntity;

                    // 3. Make it "Smart" before returning it to the UI
                    result = rawValue != null ? Entity.Wrap(rawValue, _service) : null;
                }
                else if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
                {
                    // 1. Force load the collection
                    context.Entry(_target).Collection(prop.Name).Load();

                    // 2. Collections are slightly different; we want the proxy to 
                    // track the collection itself or wrap the items.
                    result = (prop.GetValue(_target) as IEnumerable<IEntity>).Select(e => Entity.Wrap(e, _service));
                }
            }
            return result;
        }

        public void Dispose()
        {
            if (_scope != null)
            {
                _scope.Dispose();
                _scope = null;
            }
        }

    }
}
