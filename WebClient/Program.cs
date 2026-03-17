using FlashCard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.JSInterop;
using WebClient.Services;
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add device-specific services used by the FlashCard project
builder.Services.AddSingleton<IFormFactor, FormFactor>();
builder.Services.AddSingleton<ILocalServer, LocalServer>();
builder.Services.AddSingleton<ITitleService, TitleService>();
builder.Services.AddSingleton<IMenuService, MenuService>();
builder.Services.AddSingleton<IStudyService, StudyService>();
builder.Services.AddSingleton<ILoginService, LoginService>();
builder.Services.AddSingleton<ICourseService, CourseService>();
builder.Services.AddSingleton<IJsonService, StateService>();
builder.Services.AddSingleton<IStatusService, StatusService>();
builder.Services.AddSingleton<IThemeService, ThemeService>();
builder.Services.AddSingleton<IChatService, ChatService>();
builder.Services.AddScoped<HttpClient>(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddSingleton<IFileManager, FileManager>();
builder.Services.AddSingleton<IAnkiService, AnkiService>();

builder.Services.AddDbContextFactory<DataLayer.RemoteStorage>();

var app = builder.Build();
// FUCK DI
DataLayer.Utilities.RemoteQuery._service = app.Services;
FileManager._service = app.Services;
AnkiService._service = app.Services;
StatusService._service = app.Services;
ChatService._service = app.Services;

var runtime = app.Services.GetRequiredService<IJSRuntime>();
var navigation = app.Services.GetRequiredService<NavigationManager>();
var localServer = (LocalServer)app.Services.GetRequiredService<ILocalServer>();


localServer.Initialize(app, runtime, navigation);

await app.RunAsync();
