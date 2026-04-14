using Atrium.Components;
#if false
using Atrium.Extensions;
using Interfacing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;

namespace Atrium.Services;

internal abstract partial class RenderOutlet : RenderService, IDisposable, IRenderLinks
{
    protected readonly ConcurrentDictionary<Type, List<string>> RealRegistry = [];
    private readonly Lazy<MainLoader?> Main;

    //private readonly NavigationManager Nav;
    private readonly ITrustProvider Trust;
    private bool IsClosing;

    public List<string> Registry { get => [.. RealRegistry.SelectMany(list => list.Value).Distinct()]; }
    public event Action? OnChanged;

    public override Delegate ChildContent { 
        get => (RenderFragment)(__builder => BuildRenderTree(__builder)); set => base.ChildContent = value; }


    public RenderOutlet(ICompositeProvider Service, ITrustProvider _trust, Lazy<MainLoader?> _main)
        : base(Service)
    {
        //Nav = _nav;
        Main = _main;
        Trust = _trust;
        Trust.OnSettledAsync += ListenForNeeds;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
    }


    public void Register(Type who, string path)
    {
        if (RealRegistry.TryGetValue(who, out var list))
        {
            list.Add(path);
        }
        else
            RealRegistry.TryAdd(who.GetType(), [path]);
        OnChanged?.Invoke();
        InvokeAsync(StateHasChanged);
    }


    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _ = ListenForNeeds();
    }


    private void Nav_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        _ = ListenForNeeds();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        _ = ListenForNeeds();
    }

    protected abstract List<string> TypeToIncludes(Type type);


    protected virtual async Task ListenForNeeds()
    {
        if (IsClosing) return;

        Trust.OnSettledAsync += ListenForNeeds; // resubscribe for the next lazy event

        await Task.Delay(1000); // wait for layout to insert the component

        if (IsClosing) return;

        var components = Main.Value?.GetChildComponents().Select(c => c.GetType()) ?? [];

        foreach (var type in components)
        {
            var includes = type.GetInterfaces().Append(type).SelectMany(TypeToIncludes).ToList();

            if (RealRegistry.TryGetValue(type, out var list)) list.AddRange(includes);
            else RealRegistry[type] = includes;
        }


        foreach (var component in RealRegistry)
        {
            if(!components.Contains(component.Key))
            {
                RealRegistry.TryRemove(component);
            }
        }

        if (IsClosing) return;

        Main.Value?.HasChanged();

    }


    public override void Dispose()
    {
        IsClosing = true;
        //Nav.LocationChanged -= Nav_LocationChanged;
        //Trust.OnSettledAsync -= ListenForNeeds;
        base.Dispose();
    }
}

internal partial class CssOutlet(ICompositeProvider Service, ITrustProvider Trust, Lazy<MainLoader?> _main) : RenderOutlet(Service, Trust, _main)
{
    private readonly Dictionary<string, string> _filePresence = [];

    protected override List<string> TypeToIncludes(Type type) => type switch
    {
        _ when type == typeof(IHasForms) => ["_content/RazorSharp/css/accordion.css", "_content/RazorSharp/css/forms.css"],
        _ when type.Extends(typeof(LayoutComponentBase)) => ["_content/RazorSharp/css/main.css"],
        _ => []
    };



    protected override async Task ListenForNeeds()
    {
        await base.ListenForNeeds();


        foreach (var style in RealRegistry)
        {
            var names = style.Key.Assembly.GetManifestResourceNames();
            var resFile = names.Where(f => f.Contains("forms"));

            foreach(var include in style.Value)
            {
                // Use the relative path logic we discussed
                var exists = await FileSystem.AppPackageFileExistsAsync("wwwroot/" + include);
                if (exists)
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("wwwroot/" + include);
                    using var reader = new StreamReader(stream);
                    var cssString = await reader.ReadToEndAsync();
                    _filePresence[include] = cssString;
                }
            }
            
        }
    
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        foreach (var style in Registry)
        {
            if (_filePresence.TryGetValue(style, out var cssString))
            {
                builder.OpenElement(0, "style");
                builder.AddContent(1, cssString);
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(0, "link");
                builder.AddAttribute(1, "rel", "stylesheet");
                builder.AddAttribute(2, "href", style);
                builder.CloseElement();
            }
        }
    }
}

internal partial class JavascriptOutlet(ICompositeProvider Service, ITrustProvider Trust, Lazy<MainLoader?> _main) 
    : RenderOutlet(Service, Trust, _main), IJavascriptOutlet
{
    protected override List<string> TypeToIncludes(Type type) => type switch
    {
    //    _ when type is IHasForms => ["_content/RazorSharp/css/forms.css"],
        _ => []
    };

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        foreach (var style in Registry)
        {
            builder.OpenElement(0, "script");
            builder.AddAttribute(1, "type", "application/javascript");
            builder.AddAttribute(2, "src", style);
            builder.CloseElement();
        }
    }
}

#endif

