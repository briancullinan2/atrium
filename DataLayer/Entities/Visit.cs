using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("visit")]
    // Composite Index Definitions
    [Index(nameof(SessionId), nameof(UserId), Name = "session_idx")]
    [Index(nameof(Path), nameof(SessionId), nameof(UserId), Name = "path_idx")]
    [Index(nameof(Path), nameof(UserId), nameof(Created), Name = "created_idx")]
    public class Visit : Entity<Visit>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(64)]
        public string? SessionId { get; set; }

        [ForeignKey(nameof(SessionId))]
        public virtual Session? Session { get; set; }

        public string? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        [Required]
        [MaxLength(256)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Maps Doctrine 'array' to JSON. 
        /// Use 'json' for Postgres/SQL Server 2022, or string for older systems.
        /// </summary>
        public string? Query { get; set; }

        [Required]
        [MaxLength(256)]
        public string Hash { get; set; } = string.Empty;

        [Required]
        [MaxLength(8)]
        public string Method { get; set; } = string.Empty;

        public long Ip { get; set; }

        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        // Lifecycle Logic: In EF Core, we usually handle this in the 
        // DbContext or a BaseEntity, but you can set a default value here.
        public Visit()
        {
            Created = DateTime.UtcNow;
        }
    }
}