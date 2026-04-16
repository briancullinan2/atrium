
namespace UserData.Entities;

[PrimaryKey(nameof(Name), nameof(Guid), nameof(RoleId))]
[Table("setting")]
public class Setting : Entity<Setting>, IHasValue
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
    //[NotMapped]
    //public User? Setter { get; }
    //[ForeignKey(nameof(Name))]
    //public Permission? Permission { get; set; }
    //[ForeignKey(nameof(Name))]
    //public DefaultPermissions? Default { get; set; }
}
