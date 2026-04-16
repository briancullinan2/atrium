// all controls that support metadatacontrol, i.e. matching form elements by type
namespace Extensions.SlenderServices;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SupportsTypeAttribute(Type targetType) : Attribute
{
    public Type TargetType { get; } = targetType;
    public bool IncludeInherited { get; set; } = true;
}

public interface IHasMeta
{
    IEnumerable<object?>? Data { get; set; }
    PropertyMetadata? Model { get; set; }
    Action<object?>? ValueChanged { get; set; }

}
