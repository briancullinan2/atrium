using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudyModel.Entities
{
    [Table("response")]
    public class Response : Entity<Response>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        // Foreign Key for Card
        public int? CardId { get; set; }

        [ForeignKey(nameof(CardId))]
        public virtual Card? Card { get; set; }

        // Foreign Key for Answer
        public int? AnswerId { get; set; }

        [ForeignKey(nameof(AnswerId))]
        public virtual Answer? Answer { get; set; }

        // Foreign Key for User
        [Required]
        public string? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        // One-to-One relationship with File
        //        //public int? FileId { get; set; }

        // i don't know why tf this is here.
        //[ForeignKey(nameof(FileId")]
        //public virtual StudyModel.Entities.File Attachment { get; set; }

        public string? Value { get; set; }

        [Required]
        public bool IsCorrect { get; set; }

        // Equivalent to @ORM\HasLifecycleCallbacks()
        public Response()
        {
            Created = DateTime.UtcNow;
        }
    }
}