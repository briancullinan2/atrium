

namespace FlashData.Entities
{
    [Table("goal")]
    // Composite Unique Constraint: One type of goal per user (e.g., only one 'GPA' goal)
    [Index(nameof(UserId), nameof(Type), IsUnique = true, Name = "type_idx")]
    public class Goal : Entity<Goal>, IHasUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string? UserId { get; set; }

        [Required]
        [MaxLength(10)]
        public string? Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        // Keep DB column name as 'goal'
        public string? Description { get; set; } = string.Empty;

        [Required]
        public string? Reward { get; set; } = string.Empty;

        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        // Navigation property for Claims (One-to-Many)
        //public virtual ICollection<Entities.Claim> Claims { get; set; } = new HashSet<Claim>();

        public Goal()
        {
            Created = DateTime.UtcNow;
        }
    }
}