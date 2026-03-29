using DataLayer;
using DataLayer.Utilities;
using FlashCard.Services;
using FlashCard.Services.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using WebClient.Services;



AppDomain.CurrentDomain.UnhandledException += (s, e) =>
    Log.Error(e, e.ExceptionObject as Exception);

// 2. Catch exceptions in 'set and forget' tasks (Async)
TaskScheduler.UnobservedTaskException += (s, e) =>
{
    Log.Error(e, e.Exception.InnerException ?? e.Exception);
    e.SetObserved(); // Prevents process crash if you want, but logs it
};



var builder = WebAssemblyHostBuilder.CreateDefault(args);

SharedRegistry.BuildSharedServiceList(builder.Services);


builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<ITitleService, TitleService>();
builder.Services.AddSingleton<IPageManager, WebClient.Services.PageManager>();
builder.Services.AddSingleton<IHostingService, HostingService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddSingleton<IFileManager, FileManager>();
builder.Services.AddSingleton<IAnkiService, AnkiService>();

builder.Services.AddSingleton(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress.Trim('/'))
});
builder.Services.RemoveAll<IQueryManager>();

builder.Services.AddSingleton<IQueryManager, RemoteManager>();

builder.Services.AddScoped<IAuthService, BrowserStateProvider>();
builder.Services.AddScoped(sp => (BrowserStateProvider)sp.GetRequiredService<IAuthService>());

builder.Services.AddDbContextFactory<DataLayer.RemoteStorage>();
builder.Services.AddDbContextFactory<DataLayer.TestStorage>();
builder.Services.AddSingleton<ILocalStore, LocalStore>();

WebAssemblyHost? app = null;
builder.Services.AddSingleton<WebAssemblyHost>(sp => (WebAssemblyHost)app!);
app = builder.Build();
// FUCK DI
_ = app.Services.GetRequiredService<SimpleLogger>();


await app.RunAsync();
