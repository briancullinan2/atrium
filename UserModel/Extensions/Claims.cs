
namespace UserModel.Extensions
{
    public static partial class AuthenticationExtensions
    {

        public static void ConfigureClaimActions(
            this ClaimActionCollection target,
            AuthID id
        )
        {

            ICollection<Tuple<string, Func<JsonElement, string?>>> source = [];
            source.ConfigureClaimActions(id);
            foreach (var rule in source)
            {
                var claimType = rule.Item1;
                var mappingFunc = rule.Item2;

                // We use MapCustomJson as the "Universal Receiver" because 
                // your Func<JsonElement, string?> matches its signature perfectly.
                target.MapCustomJson(claimType, user => mappingFunc(user));
            }
        }



        public static void ConfigureClaimActions(
            this ICollection<Tuple<string, Func<JsonElement, string?>>> actions,
            AuthID id)
        {
            switch (id)
            {
                case AuthID.GitHub:
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    actions.MapJsonKey(ClaimTypes.Name, "name"); // or "login"
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey("urn:github:avatar", "avatar_url");
                    break;

                case AuthID.Google:
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey("urn:google:avatar", "picture");
                    break;

                case AuthID.Discord:
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    actions.MapJsonKey(ClaimTypes.Name, "global_name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");

                    actions.MapCustomJson("urn:discord:avatar", user =>
                        user.TryGetProperty("avatar", out var av) && av.GetString() != null
                        ? $"https://cdn.discordapp.com/avatars/{user.GetProperty("id").GetString()}/{av.GetString()}.png"
                        : null);
                    break;

                case AuthID.LinkedIn:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                        actions.MapJsonKey(ClaimTypes.Name, "name");
                        actions.MapJsonKey(ClaimTypes.Email, "email");
                        actions.MapJsonKey("urn:linkedin:avatar", "picture");
                    }
                    break;

                case AuthID.Twitch:
                    {
                        actions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("data")[0].GetProperty("id").GetString());
                        actions.MapCustomJson(ClaimTypes.Name, user => user.GetProperty("data")[0].GetProperty("display_name").GetString());
                        actions.MapCustomJson(ClaimTypes.Email, user => user.GetProperty("data")[0].GetProperty("email").GetString());
                        actions.MapCustomJson("urn:twitch:avatar", user => user.GetProperty("data")[0].GetProperty("profile_image_url").GetString());
                    }
                    break;

                case AuthID.Patreon:
                    {
                        actions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("data").GetProperty("id").GetString());
                        actions.MapCustomJson(ClaimTypes.Name, user => user.GetProperty("data").GetProperty("attributes").GetProperty("full_name").GetString());
                        actions.MapCustomJson(ClaimTypes.Email, user => user.GetProperty("data").GetProperty("attributes").GetProperty("email").GetString());
                    }
                    break;

                case AuthID.Trakt:
                    {
                        actions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("ids").GetProperty("slug").GetString());
                        actions.MapJsonKey(ClaimTypes.Name, "username");
                        actions.MapJsonKey("urn:trakt:vip", "vip");
                    }
                    break;

                case AuthID.BattleNet:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapJsonKey(ClaimTypes.Name, "battletag");
                    }
                    break;

                case AuthID.Strava:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapCustomJson(ClaimTypes.Name, user =>
                            $"{user.GetProperty("firstname").GetString()} {user.GetProperty("lastname").GetString()}");
                        actions.MapJsonKey("urn:strava:avatar", "profile_medium");
                    }
                    break;

                case AuthID.Reddit:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapJsonKey(ClaimTypes.Name, "name");
                        actions.MapJsonKey("urn:reddit:avatar", "icon_img");
                    }
                    break;

                case AuthID.Spotify:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapJsonKey(ClaimTypes.Name, "display_name");
                        actions.MapJsonKey(ClaimTypes.Email, "email");

                        // Spotify usually returns an array of images, this grabs the first one
                        actions.MapCustomJson("urn:spotify:avatar", user =>
                            user.TryGetProperty("images", out var images) && images.GetArrayLength() > 0
                            ? images[0].GetProperty("url").GetString()
                            : null);
                    }
                    break;

                case AuthID.Facebook:
                    // Facebook uses 'id' for the unique identifier
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "last_name");
                    // Facebook avatars require a nested 'picture' -> 'data' -> 'url' mapping
                    actions.MapCustomJson("urn:facebook:avatar", user =>
                        user.TryGetProperty("picture", out var pic) &&
                        pic.GetProperty("data").TryGetProperty("url", out var url)
                        ? url.GetString()
                        : null);
                    break;

                case AuthID.Microsoft:
                    // Microsoft (Entra/Azure AD) uses 'sub' for OIDC or 'id' for Graph API
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "family_name");
                    break;

                case AuthID.Twitter:
                    // Twitter/X uses 'id_str' to avoid JavaScript integer precision issues
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id_str");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey("urn:twitter:screenname", "screen_name");
                    actions.MapJsonKey("urn:twitter:avatar", "profile_image_url_https");
                    break;

                case AuthID.Apple:
                    // Apple is strict: 'sub' is the identifier. 
                    // IMPORTANT: Email/Name are only sent on the VERY FIRST login.
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    // Apple names are often sent in a separate 'user' JSON object during the initial post
                    actions.MapCustomJson(ClaimTypes.Name, user =>
                        user.TryGetProperty("name", out var name) &&
                        name.TryGetProperty("firstName", out var f)
                        ? $"{f.GetString()} {name.GetProperty("lastName").GetString()}"
                        : null);
                    break;

            }
        }


        public static void ConfigureBonusClaims(
            this ICollection<Tuple<string, Func<JsonElement, string?>>> actions,
            AuthID id)
        {
            switch (id)
            {
                case AuthID.Google:
                    // Requires 'profile', 'birthday', and 'addresses' scopes
                    actions.MapJsonKey(ClaimTypes.Gender, "gender");
                    actions.MapJsonKey(ClaimTypes.DateOfBirth, "birthday");
                    actions.MapCustomJson(ClaimTypes.Locality, user =>
                        user.TryGetProperty("addresses", out var addr) && addr.GetArrayLength() > 0
                        ? addr[0].GetProperty("city").GetString() : null);
                    actions.MapCustomJson(ClaimTypes.Country, user =>
                        user.TryGetProperty("addresses", out var addr) && addr.GetArrayLength() > 0
                        ? addr[0].GetProperty("country").GetString() : null);
                    break;

                case AuthID.Facebook:
                    // Requires 'user_gender', 'user_birthday', 'user_location'
                    actions.MapJsonKey(ClaimTypes.Gender, "gender");
                    actions.MapJsonKey(ClaimTypes.DateOfBirth, "birthday"); // Format: MM/DD/YYYY
                    actions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "last_name");
                    actions.MapCustomJson(ClaimTypes.Locality, user =>
                        user.TryGetProperty("location", out var loc) ? loc.GetProperty("name").GetString() : null);
                    break;

                case AuthID.Microsoft:
                    // Standard OIDC / Graph claims
                    actions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "family_name");
                    actions.MapJsonKey(ClaimTypes.Locality, "city");
                    actions.MapJsonKey(ClaimTypes.StateOrProvince, "state");
                    actions.MapJsonKey(ClaimTypes.Country, "country");
                    actions.MapJsonKey(ClaimTypes.PostalCode, "postalCode");
                    actions.MapJsonKey(ClaimTypes.MobilePhone, "mobilePhone");
                    actions.MapJsonKey(ClaimTypes.Webpage, "businessPhones"); // Often returns a list
                    break;

                case AuthID.GitHub:
                    // GitHub returns these in the core user object if public
                    actions.MapJsonKey(ClaimTypes.Locality, "location");
                    actions.MapJsonKey(ClaimTypes.Webpage, "blog");
                    actions.MapJsonKey(ClaimTypes.UserData, "bio");
                    actions.MapJsonKey("urn:github:company", "company");
                    actions.MapJsonKey("urn:github:followers", "followers");
                    break;

                case AuthID.Discord:
                    // Requires 'identify' and 'email'
                    actions.MapJsonKey("urn:discord:locale", "locale");
                    actions.MapJsonKey("urn:discord:verified", "verified");
                    actions.MapCustomJson("urn:discord:mfa", user =>
                        user.GetProperty("mfa_enabled").GetBoolean().ToString());
                    break;

                case AuthID.Twitter:
                    // Assumes v2 API /users/me
                    actions.MapJsonKey(ClaimTypes.Locality, "location");
                    actions.MapJsonKey(ClaimTypes.UserData, "description");
                    actions.MapCustomJson(ClaimTypes.Webpage, user =>
                        user.TryGetProperty("entities", out var e) && e.TryGetProperty("url", out var u)
                        ? u.GetProperty("urls")[0].GetProperty("expanded_url").GetString() : null);
                    break;

                case AuthID.Spotify:
                    // Requires 'user-read-private' and 'user-birthdate'
                    actions.MapJsonKey(ClaimTypes.Country, "country");
                    actions.MapJsonKey(ClaimTypes.DateOfBirth, "birthdate");
                    actions.MapJsonKey("urn:spotify:product", "product"); // 'premium' or 'free'
                    break;

                case AuthID.Twitch:
                    // Requires 'user:read:email'
                    actions.MapJsonKey(ClaimTypes.UserData, "description");
                    actions.MapJsonKey("urn:twitch:type", "type"); // 'staff', 'admin', 'global_mod', or ''
                    actions.MapJsonKey("urn:twitch:view_count", "view_count");
                    break;

                case AuthID.Patreon:
                    // Requires 'identity' scope
                    actions.MapCustomJson("urn:patreon:is_email_verified", user =>
                        user.GetProperty("data").GetProperty("attributes").GetProperty("is_email_verified").GetBoolean().ToString());
                    actions.MapCustomJson("urn:patreon:thumb", user =>
                        user.GetProperty("data").GetProperty("attributes").GetProperty("image_url").GetString());
                    break;

                case AuthID.Strava:
                    // Requires 'profile:read_all'
                    actions.MapJsonKey(ClaimTypes.Gender, "sex");
                    actions.MapJsonKey(ClaimTypes.Locality, "city");
                    actions.MapJsonKey(ClaimTypes.StateOrProvince, "state");
                    actions.MapJsonKey(ClaimTypes.Country, "country");
                    break;
            }
        }


        public static void MapJsonKey(this ICollection<Tuple<string, Func<JsonElement, string?>>> actions, string claimType, string jsonKey)
        {
            actions.Add(new Tuple<string, Func<JsonElement, string?>>(claimType, user =>
                user.TryGetProperty(jsonKey, out var prop) ? prop.GetString() : null));
        }

        public static void MapJsonKey(this ICollection<Tuple<string, Func<JsonElement, string?>>> actions, string claimType, Func<JsonElement, string?> jsonKey)
        {
            actions.Add(new Tuple<string, Func<JsonElement, string?>>(claimType, jsonKey));
        }

        public static void MapCustomJson(this ICollection<Tuple<string, Func<JsonElement, string?>>> actions, string claimType, Func<JsonElement, string?> jsonKey)
        {
            actions.Add(new Tuple<string, Func<JsonElement, string?>>(claimType, jsonKey));
        }
    }
}
