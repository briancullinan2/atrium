using System;
using System.Collections.Generic;
using System.Text;

// all controls that support metadatacontrol, i.e. matching form elements by type
namespace Extensions.SlenderServices
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SupportsTypeAttribute(Type targetType) : Attribute
    {
        public Type TargetType { get; } = targetType;
        public bool IncludeInherited { get; set; } = true;
    }

    public interface IHasMeta
    {
        public IEnumerable<object?>? Data { get; set; }
        public PropertyMetadata? Model { get; set; }
        public EventCallback<object?>? ValueChanged { get; set; }

    }

}
