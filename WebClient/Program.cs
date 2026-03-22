using DataLayer.Utilities;
using FlashCard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.JSInterop;
using WebClient.Services;
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddCascadingValue(sp => new ErrorBoundary());
// Add device-specific services used by the FlashCard project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<ILocalServer, LocalServer>();
builder.Services.AddSingleton<ITitleService, TitleService>();
builder.Services.AddSingleton<IMenuService, MenuService>();
builder.Services.AddSingleton<IStudyService, StudyService>();
builder.Services.AddSingleton<ILoginService, LoginService>();
builder.Services.AddSingleton<ICourseService, CourseService>();
builder.Services.AddSingleton<IPageManager, WebClient.Services.PageManager>();
builder.Services.AddSingleton<IHostingService, HostingService>();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddSingleton<IFileManager, FileManager>();
builder.Services.AddSingleton<IAnkiService, AnkiService>();
builder.Services.AddSingleton<IQueryManager, RemoteManager>();
builder.Services.AddSingleton<IAuthService, WebClient.Services.AuthService>();
builder.Services.AddSingleton<NavigationTracker>();

builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<AuthenticationStateProvider, BrowserStateProvider>();

builder.Services.AddDbContextFactory<DataLayer.RemoteStorage>();
builder.Services.AddDbContextFactory<DataLayer.TestStorage>();

var app = builder.Build();
// FUCK DI
RemoteQuery.Service = app.Services;
FileManager._service = app.Services;
AnkiService._service = app.Services;
HostingService._service = app.Services;
ChatService._service = app.Services;
QueryManager.Service = app.Services;
SimpleLogger.Services = app.Services;

var runtime = app.Services.GetRequiredService<IJSRuntime>();
var navigation = app.Services.GetRequiredService<NavigationManager>();
var localServer = (LocalServer)app.Services.GetRequiredService<ILocalServer>();


localServer.Initialize(app, runtime, navigation);

await app.RunAsync();
