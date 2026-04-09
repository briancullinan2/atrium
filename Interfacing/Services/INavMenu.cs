namespace Interfacing.Services;

public interface IMenuService
{
    //Task SetMenu(RenderFragment? menu);
    //event Action<RenderFragment?>? OnMenuChanged;

    //Task SetHeader(bool? show);
    //event Action<bool?>? OnHeaderChanged;

    List<Type> EnabledMenus { get; }
    List<Type> EnabledContexts { get; }
    List<Type> EnabledLayouts { get; }
}

public interface IHasMenu
{
    static abstract Delegate ShowMenu { get; }
    Task SetMenuActivated(bool menu);
    static abstract string Icon { get; }
}


public interface IHasLayout
{
    static abstract Delegate ShowLayout { get; }
    static abstract Delegate LayoutInsert { get; }
}


public interface IHasContext 
{
    static abstract Delegate ShowContext { get; }
    static abstract Delegate ContextInsert { get; }
}


public interface INotHasWrapper
{

}


public interface IHasService
{
    static abstract IServiceProvider Services { get; }
}

public interface IHasService<T> : IHasService
{
    static abstract T? Current { get; }
}

public interface IHasPlugins
{
    // this is the name of the setting indicating its installed or null for not
    // should return a Task<string?> 
    static abstract Delegate Installed { get; }
    // this is what the service manager should automatically add to DI if enabled after being checked for installed 
    // should return a List<Type>?
    static abstract Delegate Plugins { get; }
}

public interface IHasFeatures
{
    // this is a list of fully qualified type names of enabled features for short reference
    // should return a Task<List<string>?>
    static abstract Delegate Installed { get; }
    // this is what the UX should display after checking installed
    // should return a Dictionary<Type, string>?
    static abstract Delegate DisplayNames { get; }
}

public interface IHasSettings
{
    static abstract Delegate ShowSettings { get; }
    static abstract Delegate SettingInsert { get; }
}

public interface IHasStatus
{
    static abstract Delegate ShowStatus { get; }
    static abstract Delegate StatusInsert { get; }
}

public interface IHasHome
{
    static abstract Delegate ShowHome { get; }
    static abstract Delegate HomeInsert { get; }
}

public interface IHasPageContext
{
    static abstract Delegate ShowPageContext { get; }
    static abstract Delegate PageContextInsert { get; }
}
