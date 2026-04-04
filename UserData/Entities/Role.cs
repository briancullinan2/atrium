
namespace UserData.Entities
{
    [Table("role")]
    public class Role : Entity<Role>
    {
        [Key]
        [Category("Editable")]
        public string? Name { get; set; }
        [Category("Editable")]
        public string? Description { get; set; }
        public int Priority { get; set; }

        [InverseProperty(nameof(User.Roles))]
        public ICollection<User> Users { get; set; } = new HashSet<User>();

        [InverseProperty(nameof(Group.Roles))]
        public ICollection<Group> Groups { get; set; } = new HashSet<Group>();

        [NotMapped]
        public DefaultRoles? DefaultRole => Name?.TryParse<DefaultRoles>();

    }
}
