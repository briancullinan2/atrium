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

    public interface IEntity<T> : IEntity where T : IEntity
    {
    }

    abstract public class Entity : DispatchProxy, INotifyPropertyChanged
    {
        protected abstract override object? Invoke(MethodInfo? targetMethod, object?[]? args);
        public event PropertyChangedEventHandler? PropertyChanged;

        public static ProxyEntity<T> Wrap<T>(IEntity<T> target, IServiceProvider service, Type? context = null) where T : IEntity
        {
            return Wrap((IEntity)target, service, context) as ProxyEntity<T>;
        }

        public static ProxyEntity<T> Wrap<T>(IEntity<T> target, IServiceProvider service) where T : IEntity
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

    public class Entity<T> : IEntity<T> where T : IEntity
    {

    }

    public class ProxyEntity<T> : Entity, IEntity<T>, INotifyPropertyChanged where T : IEntity
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ProxyEntity() : base()
        {
            // observables don't like this because of invoke recursion bla bla bla, damn gemini is chatty
            // _target = this;
        }

        public T _target = default!;
        public IServiceProvider? _service = default!;
        public Type? _context = default!;

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


        public override bool Equals(object? obj)
        {
            if (this == null || obj == null) return this == obj;

            var type = typeof(T);
            //var ignoreList = new List<string>(ignore);
            var foreignKeys = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => Attribute.GetCustomAttribute(p, typeof(ForeignKeyAttribute))?.TypeId);

            // Get properties that are NOT virtual (Nav properties) and NOT marked [NotMapped]
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !foreignKeys.Contains(p.Name) // TODO: skip all IDs because they might not match on server
                    && p.GetGetMethod()?.IsVirtual != true
                    && !Attribute.IsDefined(p, typeof(KeyAttribute))  // TODO: don't match id fields
                    && !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    && !Attribute.IsDefined(p, typeof(ForeignKeyAttribute)) // comparing id is enough
                    && !typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                            //&& !ignoreList.Contains(p.Name)
                            );

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

    }
}
