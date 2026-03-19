using DataLayer;
using DataLayer.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FlashCard.Services
{
    public class DatabaseStateProvider(IQueryManager _query, IServiceProvider _services) : AuthenticationStateProvider
    {
        private static readonly string SessionId = "AtriumSession";

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Get the SessionID from the browser's cookie
            var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;

            string? sessionId = null;
            if(_services.GetService<IHttpContextAccessor>() is IHttpContextAccessor _httpContextAccessor)
            {
                sessionId = _httpContextAccessor.HttpContext?.Request.Cookies[cookieName];
            }
            else
            {
                var autoLoginSetting = await _query.Query<DataLayer.Entities.Setting>(s =>
                    s.Permission != null && s.Permission.Default == DefaultPermissions.ApplicationAutoLogin);
                sessionId = autoLoginSetting?.FirstOrDefault()?.Value;
            }

            if (string.IsNullOrEmpty(sessionId))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            // 2. Fetch the session from your DataLayer.Entities.Session table
            var session = await _query.Query<DataLayer.Entities.Session>(s => 
                s.Id == sessionId && s.Time + TimeSpan.FromSeconds(s.Lifetime) > DateTime.UtcNow);

            if (session.FirstOrDefault() == null)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            // 3. Deserialize the 'Value' (JSON) into Claims
            var identity = JsonSerializer.Deserialize<dynamic>(session.First().Value);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }



        public async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            // 1. Serialize the Claims into a JSON blob for the 'Value' column
            // We extract the claims into a simple dictionary so System.Text.Json plays nice
            var claimsData = user.Claims.ToList();

            // 3. Create the Entity
            var newSession = new DataLayer.Entities.Session();
            claimsData.Add(new Claim(nameof(SessionId), newSession.Id)); // Add the "Bridge"
            var sessionValue = JsonSerializer.Serialize(claimsData.Select(c => new { c.Type, c.Value }), JsonHelper.Default);
            newSession.Value = sessionValue;

            // 4. Save to DB using your QueryManager (assuming you have a Save/Command method)
            // If your QueryManager only does reads, you'll want to invoke your DbContext here
            await _query.Save(newSession);

            // 5. Write the Cookie so the browser remembers the ID for the next request
            var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;
            if (_services.GetService<IHttpContextAccessor>() is IHttpContextAccessor _httpContextAccessor)
            {
                _httpContextAccessor.HttpContext?.Response.Cookies.Append(cookieName, newSession.Id, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // Arizona: Always use Secure in production
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddSeconds(newSession.Lifetime)
                });
            }

            // 6. Update the identity to include the SessionId before notifying Blazor
            var claims = user.Claims.ToList();

            var newIdentity = new ClaimsIdentity(claims, typeof(DatabaseStateProvider).Name);
            var newUser = new ClaimsPrincipal(newIdentity);

            // Now notify the world with the user that actually HAS the SessionId
            var authState = Task.FromResult(new AuthenticationState(newUser));
            NotifyAuthenticationStateChanged(authState);
        }


        public static AuthenticationBuilder BuildAuthentication(IHostApplicationBuilder builder)
        {
            return builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/access-denied";

                    var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;
                    options.Cookie.Name = cookieName;
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = async context =>
                        {
                            // This runs on every request to 'Sync' the cookie with the DB
                            var query = context.HttpContext.RequestServices.GetRequiredService<IQueryManager>();
                            var sessionId = context.Principal?.FindFirst(nameof(SessionId))?.Value;

                            if (string.IsNullOrEmpty(sessionId))
                            {
                                context.RejectPrincipal();
                                return;
                            }

                            // Query your Session entity
                            var session = await query.Query<DataLayer.Entities.Session>(s => s.Id == sessionId);
                            var activeSession = session.FirstOrDefault();

                            if (activeSession == null || (activeSession.Time + TimeSpan.FromSeconds(activeSession.Lifetime)) < DateTimeOffset.UtcNow)
                            {
                                context.RejectPrincipal(); // Force Logout if DB session is gone or expired
                            }
                        }
                    };
                });
        }

    }
}
