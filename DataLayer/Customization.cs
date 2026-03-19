using System.ComponentModel;
using System.Reflection;

namespace DataLayer
{
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
    public enum StorageType : int
    {
        Ephemeral = 0,
        Persistent = 1,
        Remote = 2,
        Test = 3
    }



    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum GradeScale : int
    {
        Unset = 0,
        AthroughF = 1,
        AthroughFPlusMinus = 2
    }



    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum StepMode { Intro = 0, Video = 1, Quiz = 2, Reward = 3, Investment = 4 }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum DisplayType : int
    {
        Unset = 0,
        Text = 1,
        Image = 2,
        Video = 3,
        Audio = 4,
        Format = 5
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

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum PackMode : int
    {
        [Description("pack")] // show only one card at a time and return to home
        Card = 1,
        [Description("quiz")] // show a few cards at a time and return to home
        Quiz = 2,
        [Description("funnel")] // show a few cards then go to the next step
        Multi = 3,
        [Description("demo")] // show a few choice cards and go to store page
        Demo = 4,
        [Description("shuffle")] // randomize the cards that are due with subscriptions
        Shuffle = 5
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum CardType : int
    {
        Unset = 0,
        FlashCard = 1,
        TrueFalse = 2,
        Multiple = 3,
        Short = 4,
        Match = 5,
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum PackStatus : int
    {
        Unset = 0,
        Unpublished = 1,
        Published = 2,
        Public = 3,
        Unlisted = 4,
        Deleted = 5
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum Gender : int
    {
        Female = 1,
        Male = 2,
        Other = 3,
        Unspecified = 0
    }

}
