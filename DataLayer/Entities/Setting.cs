using DataLayer.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [PrimaryKey(nameof(Name), nameof(Guid), nameof(RoleId))]
    [Table("setting")]
    public class Setting : Entity<Setting>
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? RoleId { get; set; }
        [ForeignKey(nameof(RoleId))]
        public Role? Role { get; set; }
        public string? Guid { get; set; }
        [ForeignKey(nameof(Guid))]
        public User? User { get; set; }
        public string? SetterId { get; set; }
        [ForeignKey(nameof(SetterId))]
        public User? Setter { get; set; }
        [ForeignKey(nameof(Permission.Name))]
        private Permission? _permission;
        public Permission? Permission
        {
            get => _permission ?? new() { Name = Name };
            set
            {
                Name = value?.Name;
                _permission = value;
            }
        }
        [NotMapped]
        public DefaultPermissions? Default { get => Name?.TryParse<DefaultPermissions>(); set => Name = value.ToString(); }
    }
}
