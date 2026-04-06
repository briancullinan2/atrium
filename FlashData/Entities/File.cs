
namespace FlashData.Entities
{
    [Table("file")]
    [Index(nameof(Source), Name = "IX_File_Source")]
    public class File : Entity<File>, IHasUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(256)]
        public string Filename { get; set; } = string.Empty;

        [StringLength(256)]
        public string UploadId { get; set; } = string.Empty;

        [StringLength(256)]
        public string? Url { get; set; }

        // Doctrine 'array' type is best handled as a JSON string in EF Core
        public string? PartsJson { get; set; } = "[]";

        public DateTime Created { get; set; } = DateTime.UtcNow;

        // Relationship: Many Files to One User
        public string? UserId { get; set; }

        public virtual string? Source { get; set; }

        // Relationship: One File to One Response (Mapped by 'file' in Response entity)
        // don't know wtf this was for?
        //public virtual Entities.Response? Response { get; set; }

        public File()
        {
            Created = DateTime.UtcNow;
        }
    }
}