
namespace RazorSharp.Layout;


public class NavMenuItem<TComponent> : INavMenuItem
    where TComponent : class, new()
{
    public NavMenuItem() { }
    public string Title { get; set; } = string.Empty;
    virtual public string Href => TypeExtensions.GetUri(Uri);
    public Expression<Func<TComponent, TComponent>>? Uri { get; set; } = c => new TComponent();
    public string Icon { get; set; } = "bi-circle";
    public string? RoleRequired { get; set; }
    public bool IsBeta { get; set; } = false;
    public bool IsCollapsed { get; set; } = true; // Added state
    public virtual DefaultPermissions? Permission { get => RequiredPermission?.TryParse<DefaultPermissions>(); set => RequiredPermission = value.ToString(); }
    public virtual string? RequiredPermission { get; set; } = nameof(DefaultPermissions.Unset);
    public virtual List<INavMenuItem> Children { get; set; } = [];
}
