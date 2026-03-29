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
            Services.AddScoped<IMenuService, MenuService>();
            Services.AddScoped<IStudyService, StudyService>();
            Services.AddScoped<ILoginService, LoginService>();
            Services.AddScoped<ICourseService, CourseService>();
            Services.AddScoped<IPageManager, PageManager>();
            Services.AddScoped<IThemeService, ThemeService>();
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
