using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("response")]
    public class Response : Entity<Response>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        // Foreign Key for Card
        [Column("card_id")]
        public int? CardId { get; set; }

        [ForeignKey("CardId")]
        public virtual Card? Card { get; set; }

        // Foreign Key for Answer
        [Column("answer_id")]
        public int? AnswerId { get; set; }

        [ForeignKey("AnswerId")]
        public virtual Answer Answer { get; set; }

        // Foreign Key for User
        [Required]
        [Column("user_id")]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        // One-to-One relationship with File
        //[Column("file_id")]
        //public int? FileId { get; set; }

        // i don't know why tf this is here.
        //[ForeignKey("FileId")]
        //public virtual DataLayer.Entities.File Attachment { get; set; }

        [Column("value")]
        public string Value { get; set; }

        [Required]
        [Column("correct")]
        public bool IsCorrect { get; set; }

        // Equivalent to @ORM\HasLifecycleCallbacks()
        public Response()
        {
            Created = DateTime.UtcNow;
        }
    }
}