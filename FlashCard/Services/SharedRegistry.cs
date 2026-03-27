using DataLayer.Utilities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
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
            Services.AddSingleton<IMenuService, MenuService>();
            Services.AddSingleton<IStudyService, StudyService>();
            Services.AddScoped<ILoginService, LoginService>();
            Services.AddSingleton<ICourseService, CourseService>();
            Services.AddSingleton<IPageManager, PageManager>();
            Services.AddSingleton<IThemeService, ThemeService>();
            Services.AddSingleton<IQueryManager, QueryManager>();
            //Services.AddScoped<IAuthService, AuthService>();
            Services.AddScoped<NavigationTracker>();
            Services.AddSingleton<SimpleLogger>();


            Services.AddAuthorizationCore();
            Services.AddCascadingAuthenticationState();

            Services.AddScoped(sp => (AuthenticationStateProvider)sp.GetRequiredService<IAuthService>());
            Services.AddScoped(sp => (AuthService)sp.GetRequiredService<IAuthService>());


        }
    }
}
