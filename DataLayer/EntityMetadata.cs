using DataLayer.Utilities.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace DataLayer
{
    public class PropertyMetadata
    {
        // The "Old Object" we are wrapping
        protected readonly PropertyInfo _info;

        public IEnumerable<Attribute> Custom { get; private set; }

        public string Name => _info.Name;
        public Type PropertyType => _info.PropertyType;
        public MemberTypes MemberType => _info.MemberType;
        public Type? DeclaringType => _info.DeclaringType;

        // Your custom extended properties
        public int? MaxLength { get; private set; }
        public string DisplayName { get; private set; }
        public string? GroupName { get; private set; }
        public int? Order { get; private set; }
        public string? Category { get; private set; }
        public DataType? DataType { get; private set; }

        public PropertyMetadata(PropertyInfo info)
        {
            _info = info ?? throw new ArgumentNullException(nameof(info));

            // Initialize your custom lookups once here
            Custom = _info.GetCustomAttributes();
            MaxLength = _info.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? _info.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
            DisplayName = _info.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? _info.Name;
            GroupName = _info.GetCustomAttribute<DisplayAttribute>()?.GetGroupName();
            Category = _info.GetCustomAttribute<CategoryAttribute>()?.Category;
            Order = _info.GetCustomAttribute<DisplayAttribute>()?.GetOrder();
            DataType = _info.GetCustomAttribute<DataTypeAttribute>()?.DataType;
        }

        // You can even wrap the actual Get/Set calls
        public object? GetValue(object obj)
        {
            return _info.GetValue(obj);
        }

        public void SetValue(object obj, object? value)
        {
            _info.SetValue(obj, value);
        }
    }

    //[AttributeUsage(AttributeTargets.Class)]
    //public class GenerateIndexerPropertiesAttribute : Attribute { }
    //[GenerateIndexerProperties]
    public class AttributeValueIndexer<TValue>(IEnumerable<PropertyMetadata> source, Func<PropertyMetadata, TValue> selector)
    {
        private readonly IEnumerable<PropertyMetadata> _source = source;
        private readonly Func<PropertyMetadata, TValue> _selector = selector;
        private readonly Dictionary<string, TValue?> _cache = [];

        public TValue? this[string? propertyName]
        {
            get
            {
                if (propertyName == null) return default;
                if (_cache.TryGetValue(propertyName, out var value)) return value;

                var prop = _source.FirstOrDefault(p => p.Name == propertyName);
                return _cache[propertyName] = (prop != null) ? _selector(prop) : default;
            }
        }
    }
    public class AttributeIndexer(IEnumerable<PropertyMetadata> source, Func<PropertyMetadata, string?> selector)
    {
        private readonly IEnumerable<PropertyMetadata> _source = source;
        private readonly Func<PropertyMetadata, string?> _selector = selector;
        private readonly Dictionary<string, ObservableCollection<PropertyMetadata>> _cache = [];

        public ObservableCollection<PropertyMetadata>? this[string key]
        {
            get
            {
                if (_cache.TryGetValue(key, out var list)) return list;

                var props = _source.Where(p => _selector(p) == key);
                if (props.Any()) return new ObservableCollection<PropertyMetadata>(props);
                return null;

            }
        }
    }

    public class AttributeTypeIndexer(IEnumerable<PropertyMetadata> source)
    {
        private readonly IEnumerable<PropertyMetadata> _source = source;
        private readonly Dictionary<string, ObservableCollection<Attribute>> _cache = [];

        public ObservableCollection<Attribute>? this[string key]
        {
            get
            {
                if (_cache.TryGetValue(key, out var list)) return list;
                // TODO: compare types the attribute is on like Types[string]
                IEnumerable<Attribute> attributes = _source
                                  .Select(obj => (p: obj, match: obj.Custom.FirstOrDefault(attr => attr.GetType().Name.Replace("Attribute", "") == key)))
                                  .Where(obj => obj.match != null)
                                  .Select(obj => obj.match)
                                  .OfType<Attribute>();
                if (attributes.Any()) return new ObservableCollection<Attribute>(attributes);
                return null;

            }
        }
    }

    public partial class EntityMetadata //: INotifyPropertyChanged
    {
        public EntityMetadata? this[string key] => typeof(EntityMetadata).GetProperty(key, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as EntityMetadata;

        public ObservableCollection<PropertyMetadata> AllProperties { get; private set; }
        public ObservableCollection<PropertyMetadata> Uncategorized { get; private set; }
        public AttributeIndexer Groups { get; private set; }
        public AttributeIndexer Categories { get; private set; }
        public AttributeIndexer Ungrouped { get; private set; }
        public AttributeValueIndexer<int?> MaxLength { get; private set; }
        public AttributeTypeIndexer Attributes { get; private set; }
        public Type EntityType { get; private set; }
        public string TableName { get; private set; }


        internal EntityMetadata(Type entityType) : base()
        {
            EntityType = entityType;

            var props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                             .Select(p => new { p, attr = p.GetCustomAttribute<DisplayAttribute>() })
                                             .OrderBy(x => x.attr?.GetOrder() ?? 1000)
                                             .Select(x => x.p)
                                             .ToList();

            AllProperties = new ObservableCollection<PropertyMetadata>(props.Select(p => new PropertyMetadata(p)));
            Uncategorized = new ObservableCollection<PropertyMetadata>(AllProperties.Where(p => string.IsNullOrWhiteSpace(p.Category) && string.IsNullOrWhiteSpace(p.GroupName)));

            // Nested Indexers for the XAML [Brackets]
            TableName = entityType.GetCustomAttributes().OfType<TableAttribute>().FirstOrDefault()?.Name ?? entityType.Name;
            Groups = new AttributeIndexer(AllProperties, p => p.GroupName);
            Categories = new AttributeIndexer(AllProperties, p => p.Category);
            Ungrouped = new AttributeIndexer(AllProperties.Where(p => string.IsNullOrWhiteSpace(p.GroupName)), p => p.Category);
            MaxLength = new AttributeValueIndexer<int?>(AllProperties, p => p.MaxLength);
            Attributes = new AttributeTypeIndexer(AllProperties);
        }

        // TODO: ? i guess update if the attribute in the change?
        //public event PropertyChangedEventHandler? PropertyChanged;
    }

    // TODO: syntax sugar to allow for EntityMetadata.User.MaxLength[x => x.Name]
    // LIMITED TO: where TReturn : struct because only primitives and enums are allowed inside attributes and that's what we're matching mostly
    public class ModelAccessor<TModel, TReturn>(EntityMetadata<TModel> model, Func<EntityMetadata<TModel>, string?, TReturn?> selector) where TModel : Entities.Entity<TModel>, Entities.IEntity<TModel> where TReturn : struct
    {
        private readonly EntityMetadata<TModel> _model = model;
        // TODO: this is kind of single purpose
        private readonly Func<EntityMetadata<TModel>, string?, TReturn?> _selector = selector;

        // TODO: this is kind of single purpose
        // Indexer taking a function/delegate
        public TReturn? this[Expression<Func<TModel, dynamic?>> property]
        {
            get
            {
                return _selector(_model, property.FindProperty()?.Name);
            }
        }

        // Indexer taking a function/delegate
        public TReturn? this[string? property]
        {
            get
            {
                return _selector(_model, property);
            }
        }
    }

    public class EntityMetadata<T> : EntityMetadata where T : Entities.Entity<T>, Entities.IEntity<T>
    {
        new public ModelAccessor<T, int> MaxLength;
        public EntityMetadata() : base(typeof(T))
        {
            // TODO: this is kind of single purpose
            MaxLength = new ModelAccessor<T, int>(this, (md, property) => base.MaxLength[property]);
        }
    }


}
