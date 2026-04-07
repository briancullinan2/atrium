using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Hosting.Services;

public interface IComponentService
{
    Task<string> GetRenderedHtml<TComponent>(IDictionary<string, object?> parameters) where TComponent : IComponent;
}

public class RenderComponent(IServiceProvider sp, ILoggerFactory lf) : IComponentService
{
    public async Task<string> GetRenderedHtml<TComponent>(IDictionary<string, object?> parameters) where TComponent : IComponent
    {
        using var renderer = new HtmlRenderer(sp, lf);
        return await renderer.Dispatcher.InvokeAsync(async () => {
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }


    public static async Task<string> RenderComponentToHtml<TComponent>(IDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        // 1. Set up a minimalist service provider
        var services = new ServiceCollection();
        services.AddLogging();
        //services.AddSingleton<NavigationManager, HeadlessNavigationManager>();
        // Add your StudySauce/FlashCard services here

        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // 2. The HtmlRenderer is the engine
        using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);

        // 3. Components MUST run on the renderer's dispatcher
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters)
            );

            return output.ToHtmlString();
        });
    }
}
