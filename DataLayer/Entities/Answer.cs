using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("answer")]
    public class Answer : Entity<Answer>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("card_id")]
        public int CardId { get; set; }

        [ForeignKey(nameof(CardId))]
        public virtual Card? Card { get; set; }

        [Column("content", TypeName = "text")]
        [Required]
        public string? Content { get; set; }

        [Column("response", TypeName = "text")]
        public string? ResponseText { get; set; }

        [Column("value", TypeName = "text")]
        public string? Value { get; set; }

        [Column("correct")]
        public bool IsCorrect { get; set; } = false;

        [Column("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [Column("modified")]
        public DateTime? Modified { get; set; }

        [Column("deleted")]
        public bool IsDeleted { get; set; } = false;

        // Relationship: One Answer has many recorded Responses
        //public virtual ICollection<Response> Responses { get; set; } = new HashSet<Response>();
    }
}
