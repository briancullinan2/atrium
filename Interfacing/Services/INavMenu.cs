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

public interface IHasPageContext
{
    static abstract Delegate ShowPageContext { get; }
    static abstract Delegate PageContextInsert { get; }
}
