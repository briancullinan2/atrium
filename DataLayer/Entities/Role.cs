using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
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
        public ICollection<DataLayer.Entities.User> Users { get; set; } = new HashSet<DataLayer.Entities.User>();
        [InverseProperty(nameof(Group.Roles))]
        public ICollection<DataLayer.Entities.Group> Groups { get; set; } = new HashSet<DataLayer.Entities.Group>();
    }
}
