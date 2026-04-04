
namespace StudyModel.Entities
{
    [Table("answer")]
    public class Answer : Entity<Answer>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? CardId { get; set; }

        [ForeignKey(nameof(CardId))]
        public virtual Card? Card { get; set; }

        [Required]
        public string? Content { get; set; }

        //        //public string? ResponseText { get; set; }

        public string? Value { get; set; }

        public bool IsCorrect { get; set; } = false;

        public DateTime Created { get; set; } = DateTime.UtcNow;

        public DateTime? Modified { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Relationship: One Answer has many recorded Responses
        //public virtual ICollection<Response> Responses { get; set; } = new HashSet<Response>();
    }
}
