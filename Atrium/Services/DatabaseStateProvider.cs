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
    internal class DatabaseStateProvider(IQueryManager _query
#if WINDOWS
        , IHttpContextAccessor? _httpContextAccessor = null
#endif
    ) : AuthService {

        private static readonly string SessionId = "AtriumSession";

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Get the SessionID from the browser's cookie
            var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;

            string? sessionId = null;
#if WINDOWS
            if (_httpContextAccessor is not null)
            {
                sessionId = _httpContextAccessor.HttpContext?.Request.Cookies[cookieName];
            }
            else
#endif
            {
                var currentSetting = await _query.Query<Setting>(s =>
                    s.Name == nameof(DefaultPermissions.ApplicationCurrentUser))
                    .FirstOrDefaultAsync();
                sessionId = currentSetting?.Value;

                if (sessionId == null)
                {
                    var autoLoginSetting = await _query.Query<Setting>(s =>
                        s.Name == nameof(DefaultPermissions.ApplicationAutoLogin))
                        .FirstOrDefaultAsync();
                    sessionId = autoLoginSetting?.Value;
                }
            }

            if (string.IsNullOrEmpty(sessionId))
                return LoginService.Guest();

            // 2. Fetch the session from your DataLayer.Entities.Session table
            var session = await _query.Query<Session>(s =>
                s.Id == sessionId && s.Time.AddSeconds(s.Lifetime) > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (session == null)
                return LoginService.Guest();

            // 3. Deserialize the 'Value' (JSON) into Claims
            var sessionEntity = session;
            var storedClaims = JsonSerializer.Deserialize<List<UserClaim>>(sessionEntity.Value) ?? [];

            // 4. Sync Logic with Throttling (e.g., once every 30 minutes)
            var token = storedClaims.FirstOrDefault(c => c.Type == "access_token")?.Value;
            var providerStr = storedClaims.FirstOrDefault(c => c.Type == "urn:atrium:provider")?.Value;

            // Calculate how long since the last sync
            var lastSync = sessionEntity.Time;
            var needsSync = (DateTime.UtcNow - lastSync).TotalMinutes > 30;

            if (needsSync && !string.IsNullOrEmpty(token) && Enum.TryParse<AuthID>(providerStr, out var providerId))
            {
                try
                {
                    using var json = await GetFreshUserInfo(providerId, token);

                    if (json != null)
                    {
                        var freshName = json.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
                        if (freshName != null)
                        {
                            storedClaims.RemoveAll(c => c.Type == ClaimTypes.Name);
                            storedClaims.Add(new UserClaim(ClaimTypes.Name, freshName));
                        }

                        // Update the DB session and reset the timer
                        sessionEntity.Value = JsonSerializer.Serialize(storedClaims);
                        sessionEntity.Time = DateTime.UtcNow;
                        await _query.Save(sessionEntity);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't kill the session; let them use the cached claims
                    // to prevent an auth loop if the external provider is down.
                    Console.WriteLine($"Sync failed: {ex.Message}");
                }
            }

            var claims = storedClaims?.Select(c => new Claim(c.Type, c.Value));
            var providerName = storedClaims?.FirstOrDefault(c => c.Type == "urn:atrium:provider")?.Value ?? typeof(DatabaseStateProvider).Name;
            var identity = new ClaimsIdentity(claims, providerName);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }


        public record UserClaim(string Type, string Value);

        public override async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            var claimsData = user.Claims.ToList();

            var newSession = new Session();
            claimsData.Add(new Claim(nameof(SessionId), newSession.Id)); // Add the "Bridge"

            var provider = user.Identity?.AuthenticationType ?? typeof(DatabaseStateProvider).Name;
            claimsData.Add(new Claim("urn:atrium:provider", provider));

            var cookieName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? SessionId;
#if WINDOWS
            if (_httpContextAccessor is not null)
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

            var guid = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value;
            var userEntity = await _query.Query<User>(u => u.Guid == guid).FirstOrDefaultAsync<User>();
            var currentSetting = await _query.Query<Setting>(s =>
                s.Name != null
                && s.Name == nameof(DefaultPermissions.ApplicationCurrentUser))
                .FirstOrDefaultAsync()
                ?? new Setting
                {
                    Name = nameof(DefaultPermissions.ApplicationCurrentUser)
                };
            currentSetting.Value = newSession.Id;
            currentSetting.Guid = userEntity?.Guid;
            currentSetting.User = userEntity;
            //currentSetting.Default = DefaultPermissions.ApplicationCurrentUser;
            //currentSetting.Permission = await new Permission { Name = DefaultPermissions.ApplicationCurrentUser.ToString() }.Update();

            if (user.Claims.Any(c => c.Type == ClaimTypes.Role))
            {
                currentSetting.Role = await new Role { Name = user.Claims.First(c => c.Type == ClaimTypes.Role).Value }.Update(_query);
                currentSetting.RoleId = currentSetting.Role.Name;
            }

            await _query.Save(currentSetting);

            var claims = user.Claims.ToList();

            var identity = new ClaimsIdentity(claims, provider);
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));

            NotifyAuthenticationStateChanged(authState);
        }


        public static async Task<JsonDocument?> GetFreshUserInfo(AuthID providerId, string accessToken)
        {

            var (_, _, userInfoUrl) = GetOAuthEndpoints(providerId);

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
        public static void BuildAuthentication(IServiceCollection Services)
        {
            // Define a constant for the claim type to avoid naming mismatches
            const string SessionIdClaimType = "atrium_sid";

            var authenticationBuilder = Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/access-denied";

                    // Use a distinct name for the Auth Cookie vs your internal Session ID
                    var product = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Atrium";
                    options.Cookie.Name = $"{product}_Auth";

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = async context =>
                        {
                            var query = context.HttpContext.RequestServices.GetRequiredService<IQueryManager>();

                            // Look for the specific claim we injected during MarkUserAsAuthenticated
                            var sessionId = context.Principal?.FindFirst(SessionIdClaimType)?.Value;

                            if (string.IsNullOrEmpty(sessionId))
                            {
                                // No session ID found in the claims? The cookie is invalid for our DB logic.
                                context.RejectPrincipal();
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                return;
                            }

                            // Query the database to see if this session is still "Live"
                            var activeSession = await query.Query<Session>(s => s.Id == sessionId).FirstOrDefaultAsync();

                            // Validation Logic:
                            // 1. Does the session exist?
                            // 2. Has it expired based on the DB 'Time' + 'Lifetime'?
                            if (activeSession == null || (activeSession.Time.AddSeconds(activeSession.Lifetime)) < DateTime.UtcNow)
                            {
                                context.RejectPrincipal();
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            }
                        }
                    };
                });

            AddExternalLogins(authenticationBuilder);
            
        }
#endif
    }
}
