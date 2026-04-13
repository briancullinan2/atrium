using Atrium.Extensions;
using Interfacing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Storage;
using System.Net.Http;

namespace Atrium.Components;

internal partial class LogoService : RenderService, IHasCurrent<RenderFragment>, ILogoService
{
    public static RenderFragment? Current { get; set; } = __builder => __builder.AddMarkupContent(0, svgString);

    public LogoService(ICompositeProvider Service, HttpClient? _client = null)
        : base(Service)
    {
        Http = _client;
        _ = LoadSvg();
    }

    public async Task LoadSvg()
    {
        if (svgString != null) return;

        try
        {
            if (Http?.GetStringAsync("triangle.svg") is Task<string> task
                && await task is string icon)
            {
                svgString ??= icon;
            }
        }
        catch
        {

            if (await FileSystem.AppPackageFileExistsAsync("triangle.svg"))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("triangle.svg");
                using var reader = new StreamReader(stream);
                svgString ??= await reader.ReadToEndAsync();
            }
            else if (await FileSystem.AppPackageFileExistsAsync("wwwroot/triangle.svg"))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("wwwroot/triangle.svg");
                using var reader = new StreamReader(stream);
                svgString ??= await reader.ReadToEndAsync();
            }
        }
    }
    
    public static string? svgString = null;
    private readonly HttpClient? Http;
}


public partial class RenderService(ICompositeProvider service) : ComponentBase, IComponent, IDisposable
{
    protected ICompositeProvider Service { get; set; } = service;

    protected virtual RenderFragment? ChildContent
    {
        get
        {
            return __builder => {
                (GetType().GetProperty("Current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as RenderFragment)?.Invoke(__builder);
            };
        }
        set
        { }
    }

    protected RenderHandle? _renderHandle;
    public void Attach(RenderHandle renderHandle)
    {
        _renderHandle = renderHandle;
    }
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // For a service, we usually just render immediately
        Render();
        return Task.CompletedTask;
    }
    public void Render()
    {
        if (_renderHandle?.IsInitialized == true && ChildContent != null)
        {
            _renderHandle?.Render(ChildContent);
        }
    }

    public virtual void Dispose()
    {
        _renderHandle = null;
        GC.SuppressFinalize(this);
    }

    public static implicit operator RenderFragment(RenderService service)
    {
        if(service._renderHandle == null)
        {
            var main = service.Service.GetService<Lazy<MainLoader?>>();
            var handle = main?.Value?.Handle();
            //if (handle != null)
            //    service.Attach(handle.Value);
        }
        return service.ChildContent ?? (__builder => { });
    }

}
