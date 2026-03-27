using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("user")]
    public class User : Entity<User>
    {
        [Key]
        [MaxLength(256)]
        [Category("User Info")]
        [Display(Name = "Globally Unique ID", Description = "Server assigned GUID for synchronization tracking")]
        public string? Guid { get; set; }

        [MaxLength(256)]
        [Category("User Info")]
        [Display(GroupName = "General Info", Order = 0, Name = "First Name", Description = "Fill in user's first name")]
        public string? FirstName { get; set; }

        [MaxLength(256)]
        [Category("User Info")]
        [Display(GroupName = "General Info", Order = 2, Name = "Last Name", Description = "Fill in user's surname")]
        public string? LastName { get; set; }

        [Category("User Info")]
        [MaxLength(2)]
        [StringLength(2, MinimumLength = 0)]
        [Display(GroupName = "General Info", Order = 1, Name = "Middle Initial", Description = "Fill in user's first letter of middle name if it exists")]
        public string? MiddleInitial { get; set; }

        [MaxLength(256)]
        [Category("User Info")]
        [Display(GroupName = "Login Info", Order = 0, Name = "User name", Description = "Fill in user's username to use at login")]
        public string? Username { get; set; }

        [MaxLength(256)]
        [Category("User Info")]
        [Display(GroupName = "Login Info", Order = 0, Name = "Email address", Description = "Fill in user's email to use at login")]
        public string? Email { get; set; }

        [MaxLength(256)]
        [Category("User Info")]
        [StringLength(16, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(GroupName = "Login Info", Order = 1, Name = "Password", Description = "Set a new password")]
        public string? Password { get; set; }

        [MaxLength(256)]
        [NotMapped]
        [Category("User Info")]
        [DataType(DataType.Password)]
        [Display(GroupName = "Login Info", Order = 2, Name = "Confirm Password", Description = "Confirm a new password")]
        public string? Confirm { get; set; }

        public int CardsCompleted { get; set; } = 0;
        // --- Added Missing Fields from PHP Entity ---

        [Category("Metadata")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [Category("Metadata")]
        public DateTime? LastVisit { get; set; }

        [MaxLength(256)]
        [Category("External IDs")]
        public string? FacebookId { get; set; }

        [MaxLength(512)]
        [Category("External IDs")]
        public string? FacebookAccessToken { get; set; }

        [MaxLength(256)]
        [Category("External IDs")]
        public string? GoogleId { get; set; }

        [MaxLength(512)]
        [Category("External IDs")]
        public string? GoogleAccessToken { get; set; }

        [MaxLength(256)]
        [Category("External IDs")]
        public string? EvernoteId { get; set; }

        [MaxLength(512)]
        [Category("External IDs")]
        public string? EvernoteAccessToken { get; set; }

        // photo field (One-to-One with File)
        public int? PhotoFileId { get; set; }
        [ForeignKey(nameof(PhotoFileId))]
        public virtual File? Photo { get; set; }
        // like out of google profile if they logged in that way
        public virtual string? PhotoHosted { get; set; }
        public virtual string? AvatarColor { get; set; }

        public int? DownloadTokens { get; set; } = 30;

        // --- Navigation Properties (One-to-Many) ---

        //public virtual ICollection<Payment> Payments { get; set; } = new HashSet<Payment>();
        //public virtual ICollection<Visit> Visits { get; set; } = new HashSet<Visit>();
        //public virtual ICollection<Invite> InvitesSent { get; set; } = new HashSet<Invite>();
        //public virtual ICollection<Invite> InvitesReceived { get; set; } = new HashSet<Invite>();
        public virtual ICollection<Pack> AuthoredPacks { get; set; } = new HashSet<Pack>();
        public virtual ICollection<UserPack> UserPacks { get; set; } = new HashSet<UserPack>();

        [InverseProperty(nameof(File.User))]
        public virtual ICollection<File> Files { get; set; } = new HashSet<File>();
        [InverseProperty(nameof(Response.User))]
        public virtual ICollection<Response> Responses { get; set; } = new HashSet<Response>();

        [InverseProperty(nameof(Group.Users))]
        public virtual ICollection<Group> Groups { get; set; } = [];

        [InverseProperty(nameof(Role.Users))]
        public virtual ICollection<Role> Roles { get; set; } = [];


        // --- Complex Properties Handling ---

        /// <summary>
        /// Stores the "properties" array from PHP as a serialized string (JSON)
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? PropertiesJson { get; set; }

        // There is no access to sessions on purpose on in DatabaseStateProvider
        //[MaxLength(256)]
        //public string? SessionId { get; set; }

        //[MaxLength(4096 * 2)]
        //public string? Session { get; set; }

        public string? Devices { get; set; }
        public string? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public User? Parent { get; set; }

        [InverseProperty(nameof(Parent))]
        public virtual ICollection<User> Children { get; set; } = [];

        [InverseProperty(nameof(Course.Users))]
        public virtual ICollection<Course> Courses { get; set; } = [];

        [InverseProperty(nameof(Setting.User))]
        public virtual ICollection<Setting> Settings { get; set; } = [];

        public User()
        {
            Created = DateTime.Now;
            Guid = System.Guid.NewGuid().ToString();
        }
    }
}