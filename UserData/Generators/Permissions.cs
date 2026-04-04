using UserData.Entities;

namespace UserData.Generators
{
    public class Permissions : IGenerator<Permission>
    {
        public static IEnumerable<Permission> Generate()
        {
            return [
                // Display Definitions
                Create(DefaultPermissions.DisplayAnimatedBackground, "Enable or disable animated pack backgrounds.", typeof(bool)),
                Create(DefaultPermissions.DisplayMatchOS, "Sync application theme with system dark/light mode settings.", typeof(bool)),
                Create(DefaultPermissions.DisplayDarkMode, "Force dark mode manually.", typeof(bool)),
                Create(DefaultPermissions.DisplayHighContrast, "Enable accessibility high-contrast theme.", typeof(bool)),
                Create(DefaultPermissions.DisplayStartOnBoot, "Launch StudySauce automatically on computer startup.", typeof(bool)),


                Create(DefaultPermissions.ApplicationDefaultUser, "Automatically log this user into the app when it starts.", typeof(string)),
                Create(DefaultPermissions.ApplicationAutoLogin, "Should use automatic login with this application instance.", typeof(string)),

                // Theme Definitions
                Create(DefaultPermissions.ApplicationBackground, "The current active background animation type."),
                Create(DefaultPermissions.ApplicationTheme, "The primary visual theme for the application."),
                Create(DefaultPermissions.ApplicationSidebar, "The theme specifically applied to the navigation sidebar."),

                // Parental Definitions
                Create(DefaultPermissions.ParentalAllowChildSubscriptions, "Allows accounts designated as 'child' to subscribe to new study packs.", typeof(bool)),

                // Telemetry Definitions
                Create(DefaultPermissions.TelemetryEnableCreators, "Share non-specific usage data with study pack creators.", typeof(bool)),
                Create(DefaultPermissions.TelemetryEnableHosts, "Share non-specific usage data with the system host.", typeof(bool)),

                // Hosting
                Create(DefaultPermissions.HostingEnabled, "Enables the P2P or Web-Access hosting functionality.", typeof(bool)),

                // Core User/Role Management
                Create(DefaultPermissions.CanModifyDefaultUsers, "Modify system-level users and initial seeds.", true),
                Create(DefaultPermissions.CanModifyDefaultRoles, "Edit global role permissions and hierarchies.", true),
                Create(DefaultPermissions.CanImpersonateUser, "Switch sessions to view the app as another user.", true),
                Create(DefaultPermissions.CanInviteUsers, "Generate registration links or bulk import users.", true),

                // Content & Data Management
                Create(DefaultPermissions.CanManagePacks, "Import, analyze, and modify study packs.", true),
                Create(DefaultPermissions.CanExecuteBackups, "Trigger manual or scheduled database backups.", true),
                Create(DefaultPermissions.CanFlushTrash, "Permanently delete items from the system trash.", true),
                Create(DefaultPermissions.CanExportData, "Export sensitive PII and study data to external files.", true),

                // Financial & Merchant
                Create(DefaultPermissions.CanViewBilling, "Access billing records and financial history.", false),
                Create(DefaultPermissions.CanManageMerchants, "Configure merchant tools and store settings.", true),
                Create(DefaultPermissions.CanIssueTokens, "Generate and distribute download or credit tokens.", true),

                // System & Security (The "Guts")
                Create(DefaultPermissions.CanManageEncryption, "Access and rotate system encryption keys.", true),
                Create(DefaultPermissions.CanMarshalData, "Lock database for marshalling and low-level sync.", true),
                Create(DefaultPermissions.CanManageHosting, "Configure network, HDD, and hosting parameters.", true),
                Create(DefaultPermissions.CanInstallAI, "Deploy and configure local or remote AI models.", true),
                Create(DefaultPermissions.CanViewSystemStatus, "View real-time activity and system health logs.", false),


                // UI Settings
                Create(DefaultPermissions.CanChangeTheme, "Modify application visual themes, backgrounds, and contrast.", true),
                Create(DefaultPermissions.CanToggleStartup, "Configure the application to launch on OS boot.", true),

                // Privacy & Safety
                Create(DefaultPermissions.CanManageParentalControls, "Set restrictions for child accounts and content subscriptions.", true),
                Create(DefaultPermissions.CanManageTelemetry, "Toggle data sharing with content creators and hosts.", true),

                // Infrastructure
                Create(DefaultPermissions.CanToggleHosting, "Enable or disable the remote study system hosting service.", true),
                Create(DefaultPermissions.CanExportSettings, "Export theme and application configuration to CSV/Clipboard.", true),

                // Absolute Override
                Create(DefaultPermissions.Unrestricted, "Superuser access. Overrides all specific checks.", true)
            ];
        }

        private static Permission Create(DefaultPermissions permission, string description, Type? type = null)
        {
            return Create(permission, description, true, type);
        }

        private static Permission Create(DefaultPermissions permission, string description, bool isActionable, Type? type = null)
        {
            return new Permission
            {
                Name = permission.ToString(),
                Description = description,
                IsActionable = isActionable,
                Type = type?.AssemblyQualifiedName ?? typeof(bool).AssemblyQualifiedName
            };
        }
    }
}
