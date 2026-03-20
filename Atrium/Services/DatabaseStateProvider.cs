using DataLayer;
using DataLayer.Utilities;
using FlashCard.Services;
#if WINDOWS
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Hosting;
#endif
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DataLayer.Generators;
using DataLayer.Entities;
using DataLayer.Utilities.Extensions;

namespace Atrium.Services
{
    public class DatabaseStateProvider(IQueryManager _query, IServiceProvider _services) : AuthenticationStateProvider
    {

        private static readonly string SessionId = "AtriumSession";

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Get the SessionID from the browser's cookie
            var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;

            string? sessionId = null;
#if WINDOWS
            if (_services.GetService<IHttpContextAccessor>() is IHttpContextAccessor _httpContextAccessor)
            {
                sessionId = _httpContextAccessor.HttpContext?.Request.Cookies[cookieName];
            }
            else
#endif
            {
                var currentSetting = await _query.Query<DataLayer.Entities.Setting>(s =>
                    s.Name != null && s.Name == DefaultPermissions.ApplicationCurrentUser.ToString());
                sessionId = currentSetting?.FirstOrDefault()?.Value;

                if (sessionId == null)
                {
                    var autoLoginSetting = await _query.Query<DataLayer.Entities.Setting>(s =>
                        s.Name != null && s.Name == DefaultPermissions.ApplicationAutoLogin.ToString());
                    sessionId = autoLoginSetting?.FirstOrDefault()?.Value;
                }
            }

            if (string.IsNullOrEmpty(sessionId))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            // 2. Fetch the session from your DataLayer.Entities.Session table
            var session = await _query.Query<DataLayer.Entities.Session>(s =>
                s.Id == sessionId && s.Time.AddSeconds(s.Lifetime) > DateTime.UtcNow);

            if (session.FirstOrDefault() == null)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            // 3. Deserialize the 'Value' (JSON) into Claims
            var sessionEntity = session.First();
            var storedClaims = JsonSerializer.Deserialize<List<UserClaim>>(sessionEntity.Value) ?? [];

            // 4. Sync Logic
            var token = storedClaims.FirstOrDefault(c => c.Type == "access_token")?.Value;
            var providerStr = storedClaims.FirstOrDefault(c => c.Type == "urn:atrium:provider")?.Value;

            if (!string.IsNullOrEmpty(token) && Enum.TryParse<AuthID>(providerStr, out var providerId))
            {
                var authService = _services.GetRequiredService<AuthService>();
                using var json = await GetFreshUserInfo(providerId, token);

                if (json != null)
                {
                    // Update claims with fresh data from the JSON
                    // You can loop through your existing MapJsonKey logic or do it manually:
                    var freshName = json.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (freshName != null)
                    {
                        storedClaims.RemoveAll(c => c.Type == ClaimTypes.Name);
                        storedClaims.Add(new UserClaim(ClaimTypes.Name, freshName));
                    }

                    // Update the DB session so we don't sync again for another hour
                    sessionEntity.Value = JsonSerializer.Serialize(storedClaims);
                    sessionEntity.Time = DateTime.UtcNow; // Reset the sync timer
                    await _query.Save(sessionEntity);
                }
            }

            var claims = storedClaims?.Select(c => new Claim(c.Type, c.Value));
            var providerName = storedClaims?.FirstOrDefault(c => c.Type == "urn:atrium:provider")?.Value ?? typeof(DatabaseStateProvider).Name;
            var identity = new ClaimsIdentity(claims, providerName);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }


        public record UserClaim(string Type, string Value);

        public async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            var claimsData = user.Claims.ToList();

            var newSession = new Session();
            claimsData.Add(new Claim(nameof(SessionId), newSession.Id)); // Add the "Bridge"

            var provider = user.Identity?.AuthenticationType ?? typeof(DatabaseStateProvider).Name;
            claimsData.Add(new Claim("urn:atrium:provider", provider));

            var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;
#if WINDOWS
            if (_services.GetService<IHttpContextAccessor>() is IHttpContextAccessor _httpContextAccessor)
            {
                var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Could not obtain Http context");
                var token = await context.GetTokenAsync("access_token");
                if (string.IsNullOrEmpty(token))
                {
                    token = await context.GetTokenAsync("refresh_token");
                }
                if (!string.IsNullOrEmpty(token))
                {
                    claimsData.Add(new Claim("access_token", token));
                }

                context.Response.Cookies.Append(cookieName, newSession.Id, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // Arizona: Always use Secure in production
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddSeconds(newSession.Lifetime)
                });
            }
#endif
            var sessionValue = JsonSerializer.Serialize(claimsData.Select(c => new UserClaim(c.Type, c.Value)), JsonHelper.Default);
            newSession.Value = sessionValue;
            // there is no newSession.User on purpose



            // TODO: set newSession.Lifetime to token lifetime
            await _query.Save(newSession);

            var guid = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userEntity = (await _query.Query<User>(u => u.Guid == guid)).FirstOrDefault();
            var currentSetting = (await _query.Query<Setting>(s =>
                s.Name != null
                && s.Name == DefaultPermissions.ApplicationCurrentUser.ToString()))?.FirstOrDefault()
                ?? new Setting
                {
                    Name = DefaultPermissions.ApplicationCurrentUser.ToString()
                };
            currentSetting.Value = newSession.Id;
            currentSetting.Guid = userEntity?.Guid;
            currentSetting.User = userEntity;
            //currentSetting.Default = DefaultPermissions.ApplicationCurrentUser;
            //currentSetting.Permission = await new Permission { Name = DefaultPermissions.ApplicationCurrentUser.ToString() }.Update();

            if (user.Claims.Any(c => c.Type == ClaimTypes.Role))
            {
                currentSetting.Role = await new Role { Name = user.Claims.First(c => c.Type == ClaimTypes.Role).Value }.Update();
                currentSetting.RoleId = currentSetting.Role.Name;
            }

            await _query.Save(currentSetting);

            var claims = user.Claims.ToList();

            var newIdentity = new ClaimsIdentity(claims, provider);
            var newUser = new ClaimsPrincipal(newIdentity);

            // Now notify the world with the user that actually HAS the SessionId
            var authState = Task.FromResult(new AuthenticationState(newUser));
            NotifyAuthenticationStateChanged(authState);
        }


        public async Task<JsonDocument?> GetFreshUserInfo(AuthID providerId, string accessToken)
        {
            var _authService = _services.GetService<IAuthService>() ?? throw new InvalidOperationException("Auth service not available.");
            var (_, _, userInfoUrl) = _authService.GetOAuthEndpoints(providerId);

            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // GitHub and others require a User-Agent
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36";
            request.Headers.UserAgent.ParseAdd(userAgent);
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Sec-Ch-Ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"144\", \"Chromium\";v=\"144\"");
            request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "none");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

#if WINDOWS
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
#endif
    }
}
