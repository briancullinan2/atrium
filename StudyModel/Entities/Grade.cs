using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudyModel.Entities
{
    [Table("grade")]
    public class Grade : Entity<Grade>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Keeping original DB column name
        public int? SubjectId { get; set; }

        [ForeignKey(nameof(SubjectId))]
        public virtual Subject Subject { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string Assignment { get; set; } = string.Empty;

        public int? Percent { get; set; }

        public int? Score { get; set; }

        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        // --- Logic & Calculated Properties ---

        [NotMapped]
        public string? LetterGrade =>
            Schedule.ConvertToScale(Subject?.Schedule?.GradeScale, Score).Letter;

        [NotMapped]
        public double? GPA =>
            Schedule.ConvertToScale(Subject?.Schedule?.GradeScale, Score).GPA;

        [NotMapped]
        public bool Deleted { get; set; } // Matches your validation logic from the PHP side

        public Grade()
        {
            Created = DateTime.UtcNow;
        }
    }
}