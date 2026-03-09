using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("group")]
    public class Group : Entity<Group>
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        [StringLength(180)]
        [Display(GroupName = "Group Info")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        [StringLength(256)]
        [Display(GroupName = "Group Info")]
        public string Description { get; set; } = string.Empty;

        [Column("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [Column("deleted")]
        [Display(GroupName = "Extended Info")]
        public bool Deleted { get; set; } = false;

        // FOSUserBundle usually stores roles as a serialized array
        // In EF, we can store this as a JSON string or a comma-separated list
        [Column("roles")]
        public string RolesJson { get; set; } = "[]";

        // Logo Relationship
        [Column("file_id")]
        public int? FileId { get; set; }

        [ForeignKey(nameof(FileId))]
        [Display(GroupName = "Group Info")]
        public virtual File? Logo { get; set; }
        [Display(GroupName = "Group Info")]
        public virtual string? LogoHosted { get; set; }

        // Self-Referencing Hierarchy (Parent/Subgroups)
        [Column("parent")]
        public int? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        [Display(GroupName = "Extended Info")]
        public virtual Group? Parent { get; set; }
        [InverseProperty(nameof(Parent))]
        public virtual ICollection<Group> Subgroups { get; set; } = new List<Group>();

        // Navigation Collections
        //public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
        //public virtual ICollection<Invite> Invites { get; set; } = new List<Invite>();

        // One-To-Many: Packs owned by this group
        [InverseProperty(nameof(Pack.Group))]
        public virtual ICollection<Pack> Packs { get; set; } = new List<Pack>();

        // Many-To-Many: Packs associated with groups
        //public virtual ICollection<Pack> GroupPacks { get; set; } = new List<Pack>();

        // Many-To-Many: Users in groups
        [InverseProperty(nameof(User.Groups))]
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        [InverseProperty(nameof(Role.Groups))]
        public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
        public Group()
        {
            Created = DateTime.UtcNow;
        }
    }
}