
namespace UserData.Entities
{
    [Table("group")]
    public class Group : Entity<Group>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(180)]
        [Display(GroupName = "Group Info")]
        public string? Name { get; set; } = string.Empty;

        [StringLength(256)]
        [Display(GroupName = "Group Info")]
        public string? Description { get; set; } = string.Empty;

        public DateTime Created { get; set; } = DateTime.UtcNow;

        [Display(GroupName = "Extended Info")]
        public bool Deleted { get; set; } = false;

        // FOSUserBundle usually stores roles as a serialized array
        // In EF, we can store this as a JSON string or a comma-separated list
        public string RolesJson { get; set; } = "[]";

        // Logo Relationship
        public int? FileId { get; set; }

        [ForeignKey(nameof(FileId))]
        [Display(GroupName = "Group Info")]
        public virtual File? Logo { get; set; }
        [Display(GroupName = "Group Info")]
        public virtual string? LogoHosted { get; set; }

        // Self-Referencing Hierarchy (Parent/Subgroups)
        public int? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        [Display(GroupName = "Extended Info")]
        public virtual Group? Parent { get; set; }
        [InverseProperty(nameof(Parent))]
        public virtual ICollection<Group> Subgroups { get; set; } = [];

        // Navigation Collections
        //public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
        //public virtual ICollection<Invite> Invites { get; set; } = new List<Invite>();

        // One-To-Many: Packs owned by this group
        [InverseProperty(nameof(Pack.Group))]
        public virtual ICollection<Pack> Packs { get; set; } = [];

        // Many-To-Many: Packs associated with groups
        //public virtual ICollection<Pack> GroupPacks { get; set; } = new List<Pack>();

        // Many-To-Many: Users in groups
        [InverseProperty(nameof(User.Groups))]
        public virtual ICollection<User> Users { get; set; } = [];
        [InverseProperty(nameof(Role.Groups))]
        public virtual ICollection<Role> Roles { get; set; } = [];
        [InverseProperty(nameof(Course.Groups))]
        public virtual ICollection<Course> Courses { get; set; } = [];

        public Group()
        {
            Created = DateTime.UtcNow;
        }
    }
}