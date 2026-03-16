namespace FlashCard.Services
{
    public interface IChatService
    {
        Task<Tuple<bool?, string?>> PingService(string ServiceUrl, string ModelName, string ApiKey, string Response, List<DynamicParam> Parameters);
        Task<List<ServicePreset>> ListPresets();
        Task<string?> SendMessage(string message);
        Task<bool?> IsWorking();
        event Action<bool?>? OnChatWorking;
        Task SetChatMode(bool chat);
        bool Chat { get; set; }
        event Action<bool>? OnChatChanged;
        Dictionary<DateTime, Tuple<bool, string>>? Recents { get; }
        event Action? OnChatMessage;
    }



    public class ChatCommand
    {
        public string Function { get; set; } = "";
        public string Parameters { get; set; } = "";
        public string Description { get; set; } = "";


        public static List<ChatCommand> CommandRegistry = new()
        {
            // --- Navigation & Context ---
            new() {
                Function = "NavigateTo",
                Parameters = "(page_name, entry_id?)",
                Description = "Redirects the user to any menu item. entry_id is used for specific courses, packs, or user profiles."
            },
            new() {
                Function = "SearchContent",
                Parameters = "(query, category_hint)",
                Description = "Filters the current view or searches global content (Packs, Courses, or Notes)."
            },

            // --- Content Management (StudySauce Core) ---
            new() {
                Function = "CreateEntity",
                Parameters = "(type [course|pack|card|group], title, parent_id?)",
                Description = "Initializes a new object. parent_id is used when adding cards to a pack or courses to a group."
            },
            new() {
                Function = "ImportData",
                Parameters = "(source_type [csv|json|pdf], target_page)",
                Description = "Triggers the import workflow for users or content packs."
            },
            new() {
                Function = "AnalyzeStudyMaterial",
                Parameters = "(entity_id, focus_area [difficulty|completeness|summary])",
                Description = "Runs the AI Analysis tool on a specific pack or course to find gaps in knowledge."
            },

            // --- User & Admin Tools ---
            new() {
                Function = "ManagePermissions",
                Parameters = "(target_user_id, permission_descriptor, action [grant|revoke])",
                Description = "Modifies user access levels or group settings."
            },
            new() {
                Function = "SyncService",
                Parameters = "(service_name [hosting|billing|ai_local])",
                Description = "Forces a status check or synchronization with remote/local provider."
            },

            // --- Study & Productivity ---
            new() {
                Function = "SetGoal",
                Parameters = "(subject_id, target_date, metric_goal)",
                Description = "Creates a new study deadline or metric goal for a specific course."
            },
            new() {
                Function = "CalculateGrades",
                Parameters = "(course_id, weight_json)",
                Description = "Opens the grade calculator with pre-filled weights for a specific class."
            },

            // --- System & Maintenance ---
            new() {
                Function = "PerformBackup",
                Parameters = "(scope [database|files|all], destination [local|cloud])",
                Description = "Triggers the backup sequence from the Content/Backups page."
            },
            new() {
                Function = "PurgeTrash",
                Parameters = "(days_old, category)",
                Description = "Permanently deletes items from the Trash bin based on age."
            },
            // --- Data Integrity & Evolution ---
            new() {
                Function = "MergeEntities",
                Parameters = "(type [user|course|pack], source_id, target_id, delete_source_bool)",
                Description = "Consolidates two items into one. Moves all children (cards/results) to the target."
            },
            new() {
                Function = "DeprecateContent",
                Parameters = "(entity_id, replacement_id?, archive_bool)",
                Description = "Marks content as obsolete. Optionally redirects users to a newer replacement_id."
            },

            // --- Communication & Outreach ---
            new() {
                Function = "EmailUsers",
                Parameters = "(group_id|user_id, template_name, urgency [low|high])",
                Description = "Sends a formatted email via the Admin/Emails service. Can target specific users or entire groups."
            },

            // --- System Awareness (The "Stati" Check) ---
            new() {
                Function = "GetSystemHealth",
                Parameters = "(category [hosting|ai|database|all])",
                Description = "Aggregates Status and Activity pages into a single health report. Checks if services are 'Up' or 'Degraded'."
            },

            // --- Content Curation ---
            new() {
                Function = "ContentCleanup",
                Parameters = "(dry_run_bool, threshold_days)",
                Description = "Identifies unlinked cards or empty packs in the Trash/Recovery area for potential purging."
            }
        };
    }



    public class ServicePreset
    {
        public bool IsDefault { get; set; } = false;
        public bool IsPrevious { get; set; } = false;
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string DefaultModel { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ResponsePath { get; set; } = "";
        public List<DynamicParam> Params { get; set; } = new();
    }

    public class DynamicParam
    {
        public string Key { get; set; } = "";
        public string Type { get; set; } = "string";
        public string Value { get; set; } = "";
        public bool BoolValue { get; set; }
    }

}
