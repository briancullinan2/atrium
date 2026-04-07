using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Extensions.SlenderServices;

public interface IAuthService
{
    Task MarkUserAsAuthenticated(ClaimsPrincipal user);
    event AuthenticationStateChangedHandler? AuthenticationStateChanged;
    Task<AuthenticationState> GetAuthenticationStateAsync();
    Task<JsonDocument?> GetFreshUserInfo(AuthID providerId, string accessToken);
    Task<string?> TryResponseToken();
}


public enum AuthType { BuiltIn, GenericOAuth, OpenIdConnect }

// Statically typed IDs for all 12+ providers
public enum AuthID
{
    Unset,
    Google,
    GitHub,
    Microsoft,
    Facebook,
    Apple,
    Discord,
    Twitter,
    LinkedIn,
    Twitch,
    Reddit,
    Okta,
    Auth0,
    Patreon,
    Spotify,
    Trakt,
    BattleNet,
    Strava
}


[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum DefaultPermissions : int
{
    Unset = 0,


    // Display Settings
    DisplayAnimatedBackground = 10,
    DisplayMatchOS = 11,
    DisplayDarkMode = 12,
    DisplayHighContrast = 13,
    DisplayStartOnBoot = 14,

    ApplicationCurrentUser = 7,
    ApplicationAutoLogin = 6,
    ApplicationDefaultUser = 5,

    // Theme Metadata (The ones using MetadataControl)
    ApplicationBackground = 15, // "Application.Background"
    ApplicationTheme = 16,      // "Application.Theme"
    ApplicationSidebar = 17,    // "Application.Sidebar"

    // UI & Experience
    CanChangeTheme = 18,        // Background, Dark Mode, High Contrast
    CanToggleStartup = 19,      // Start when computer starts

    // Parental Controls
    ParentalAllowChildSubscriptions = 20,

    // Privacy & Safety
    CanManageParentalControls = 21,
    CanManageTelemetry = 22,    // Usage statistics sharing

    // Telemetry
    TelemetryEnableCreators = 30,
    TelemetryEnableHosts = 31,

    // Infrastructure
    HostingEnabled = 40,


    // System & Infrastructure
    CanToggleHosting = 50,      // Enable/Disable web access
    CanExportSettings = 51,     // "Copy to CSV" functionality

    // Core User/Role Management
    CanModifyDefaultUsers = 1,
    CanModifyDefaultRoles = 2,
    CanImpersonateUser = 3,      // "Switch Users" functionality
    CanInviteUsers = 4,         // For User Import/Registration

    // Content & Data Management
    CanManagePacks = 100,        // Pack Import / Content Analysis
    CanExecuteBackups = 101,     // Device HDD / Backups
    CanFlushTrash = 102,         // Trash management
    CanExportData = 103,         // Data export (P.I.I. sensitive)

    // Financial & Merchant
    CanViewBilling = 200,        // Billing / Payments
    CanManageMerchants = 201,    // Merchant Tools
    CanIssueTokens = 202,        // Download Tokens / Credits

    // System & Security (The "Guts")
    CanManageEncryption = 300,   // Incognito / Keys
    CanMarshalData = 301,        // Marshalling / DB Lock
    CanManageHosting = 302,      // HDD Network / Hosting
    CanInstallAI = 303,          // Robot / AI deployment
    CanViewSystemStatus = 304,   // Status / Activity logs

    // Absolute Override
    Unrestricted = 9000
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum DefaultRoles : int
{
    Guest = 0,
    Client = 1,
    Tech = 2,
    Admin = 3
}


[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum ControlMode : int
{
    Unset = 0,
    View = 1,
    Edit = 2,
    Owner = 3,
    Group = 4,
    Result = 5,
    List = 6,
    Add = 7,
    Delete = 8,
}

