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
        public string? Type { get; set; }
        [NotMapped]
        public Type? ResolvedType
        {
            get
            {
                if(Type == null)
                    return null;
                try
                {
                    return System.Type.GetType(Type);
                }
                catch
                {
                    return null;
                }
            }
        }
        public bool IsActionable { get; set; }
        [NotMapped]
        public bool IsPageAccess { get; set; } = false;
        [NotMapped]
        public string? Simplified
        {
            get
            {
                return IsPageAccess ? simplifiedSet ?? Name : Name;
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
