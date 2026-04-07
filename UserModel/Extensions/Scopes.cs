using System;
using System.Collections.Generic;
using System.Text;

namespace UserModel.Extensions;

public static partial class AuthenticationExtensions
{

    public static void AddIdentityScopes(this ICollection<string> scopes, AuthID id)
    {

        if (id == AuthID.Google)
        {
            // Essential for OIDC and profile data
            scopes.Add("openid");
            scopes.Add("profile");
            scopes.Add("email");
        }

        if (id == AuthID.Facebook)
        {
            // 'public_profile' is default, but 'email' is separate
            scopes.Add("public_profile");
            scopes.Add("email");
        }

        if (id == AuthID.Microsoft)
        {
            // Needs these for the Graph API / UserInfo endpoint
            scopes.Add("openid");
            scopes.Add("profile");
            scopes.Add("email");
            scopes.Add("User.Read");
        }

        if (id == AuthID.Twitter)
        {
            // OAuth 2.0 (X) requires these for basic identity
            scopes.Add("users.read");
            scopes.Add("tweet.read");
        }

        if (id == AuthID.Apple)
        {
            // Apple only shares name/email on first authorize
            scopes.Add("name");
            scopes.Add("email");
        }

        if (id == AuthID.GitHub)
        {
            // 'user' gives full access, 'read:user' is the bare minimum for profile
            // 'user:email' is required to see private email addresses
            scopes.Add("read:user");
            scopes.Add("user:email");
        }

        if (id == AuthID.Discord)
        {
            // 'identify' gets ID/Avatar/Username, 'email' is separate
            scopes.Add("identify");
            scopes.Add("email");
        }

        if (id == AuthID.Reddit)
        {
            // 'identity' is the scope for profile info
            scopes.Add("identity");
        }

        if (id == AuthID.Spotify)
        {
            // 'user-read-private' for name/id, 'user-read-email' for email
            scopes.Add("user-read-private");
            scopes.Add("user-read-email");
            // You already had these for your player logic:
            scopes.Add("user-read-currently-playing");
            scopes.Add("user-read-playback-state");
        }

        if (id == AuthID.Trakt)
        {
            // Trakt is usually public, but 'public' scope is standard
            // No specific scope usually needed for basic /users/settings
        }

        if (id == AuthID.BattleNet)
        {
            // 'openid' is required for the user info handshake
            scopes.Add("openid");
        }

    }




    public static void AddBonusScopes(this ICollection<string> scopes, AuthID id)
    {
        switch (id)
        {
            case AuthID.Google:
                // Google bundles most under 'profile', but 'address' is separate
                scopes.Add("openid");
                scopes.Add("profile"); // Name, Surname, Gender, Picture
                scopes.Add("email");
                scopes.Add("https://www.googleapis.com/auth/user.addresses.read"); // Locality/PostalCode
                scopes.Add("https://www.googleapis.com/auth/user.birthday.read");  // DateOfBirth
                break;

            case AuthID.Facebook:
                scopes.Add("public_profile");
                scopes.Add("email");
                scopes.Add("user_gender");    // ClaimTypes.Gender
                scopes.Add("user_birthday");  // ClaimTypes.DateOfBirth
                scopes.Add("user_location");  // ClaimTypes.Locality/StateOrProvince
                scopes.Add("user_hometown");  // HomeAddress/Locality
                break;

            case AuthID.Microsoft:
                scopes.Add("openid");
                scopes.Add("profile");
                scopes.Add("email");
                // Graph API specific scopes for extended claims
                scopes.Add("User.Read");
                scopes.Add("User.Read.All"); // Necessary for some Group/Sid claims
                break;

            case AuthID.GitHub:
                // GitHub doesn't have a 'birthday' or 'gender' scope
                scopes.Add("read:user");
                scopes.Add("user:email");
                // No extra scopes needed; GitHub returns 'location' and 'blog' in the standard user object
                break;

            case AuthID.Discord:
                scopes.Add("identify");
                scopes.Add("email");
                // 'connections' allows you to see their linked accounts (YouTube, Twitch, etc.)
                scopes.Add("connections");
                break;

            case AuthID.LinkedIn:
                scopes.Add("openid");
                scopes.Add("profile"); // ClaimTypes.GivenName, ClaimTypes.Surname
                scopes.Add("email");
                break;

            case AuthID.Twitter:
                // Twitter (X) v2 uses specific comma-separated fields rather than "scopes" 
                // but for the OAuth handshake, these ensure access:
                scopes.Add("users.read");
                scopes.Add("tweet.read");
                break;

            case AuthID.Twitch:
                scopes.Add("user:read:email");
                // This is how you'd get the 'Webpage' claim for Twitch streamers
                scopes.Add("user:read:broadcast");
                break;

            case AuthID.Spotify:
                scopes.Add("user-read-private"); // ClaimTypes.Country
                scopes.Add("user-read-email");
                scopes.Add("user-birthdate");     // ClaimTypes.DateOfBirth
                break;

            case AuthID.Strava:
                // Strava is stingy; 'profile:read_all' is needed for full Locality data
                scopes.Add("profile:read_all");
                break;
        }
    }

}
