using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("permission")]
    public class Permission : Entity<Permission>
    {
        [Key]
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActionable { get; set; }
        [NotMapped]
        public bool IsPageAccess { get; set; } = false;
        [NotMapped]
        public string? Simplified
        {
            get
            {
                return IsPageAccess ? simplifiedSet : Name;
            }
            set
            {
                simplifiedSet = value;
            }
        }
        [NotMapped]
        private string? simplifiedSet = null;
        [NotMapped]
        public string? Baml { get; set; }
        [NotMapped]
        public System.Reflection.Assembly? Assembly { get; set; }
    }
}
