using DataLayer.Utilities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FlashCard.Services
{
    public static class SharedRegistry
    {
        public static void BuildSharedServiceList(IServiceCollection Services)
        {
            Services.AddCascadingValue(sp => new ErrorBoundary());
            // FUCK DI
            Services.AddScoped<IMenuService, MenuService>();
            Services.AddScoped<IStudyService, StudyService>();
            Services.AddScoped<ILoginService, LoginService>();
            Services.AddScoped<ICourseService, CourseService>();
            Services.AddScoped<IThemeService, ThemeService>();
            //Services.AddScoped<IAuthService, AuthService>();
            Services.AddScoped<NavigationTracker>();
            Services.AddSingleton<SimpleLogger>();


            Services.AddAuthorizationCore();
            Services.AddCascadingAuthenticationState();

            Services.AddScoped(sp => (AuthenticationStateProvider)sp.GetRequiredService<IAuthService>());
            Services.AddScoped(sp => (AuthService)sp.GetRequiredService<IAuthService>());

            // TODO: this line is for testing
            Services.AddSingleton<IQueryManager, RemoteManager>(sp => sp.GetRequiredService<RemoteManager>());
            // TODO: should be
            //Services.AddSingleton<IQueryManager, QueryManager>();
            Services.AddSingleton<RemoteManager>();

            Services.AddDbContextFactory<DataLayer.EphemeralStorage>();
            Services.AddDbContextFactory<DataLayer.PersistentStorage>();
            Services.AddDbContextFactory<DataLayer.RemoteStorage>();
            Services.AddDbContextFactory<DataLayer.TestStorage>();

            Services.AddSingleton<ILocalStore, LocalStore>();

            Services.AddScoped(sc => sc.GetRequiredService<IDbContextFactory<DataLayer.TestStorage>>().CreateDbContext());
            Services.AddScoped(sc => sc.GetRequiredService<IDbContextFactory<DataLayer.RemoteStorage>>().CreateDbContext());
            Services.AddScoped(sc => sc.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>().CreateDbContext());
            Services.AddScoped(sc => sc.GetRequiredService<IDbContextFactory<DataLayer.PersistentStorage>>().CreateDbContext());

            Services.AddSingleton<IPageManager, PageManager>();
            Services.AddSingleton<IRenderStateProvider, RenderStateProvider>();
        }
    }
}
