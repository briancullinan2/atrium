using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudyModel.Entities
{
    [Table("subject")] // Retaining table name for DB compatibility
    public class Subject : Entity<Subject>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(1)]
        public string Type { get; set; } = "c"; // 'c' for class, 'o' for other

        [MaxLength(64)]
        public string? StudyType { get; set; }

        [MaxLength(64)]
        public string? StudyDifficulty { get; set; }

        /// <summary>
        /// Days of the Week. Maps Doctrine 'simple_array' to a comma-separated string.
        /// </summary>
        [MaxLength(256)]
        public string? Dotw { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? CreditHours { get; set; }
        public string? ManualGrade { get; set; } // Renamed from 'grade' to distinguish from calculated grade

        public int ScheduleId { get; set; }
        [ForeignKey(nameof(ScheduleId))]
        public virtual Schedule Schedule { get; set; } = null!;

        public DateTime Created { get; set; } = DateTime.UtcNow;
        public bool Deleted { get; set; } = false;

        // Relationships
        //public virtual ICollection<Checkin> Checkins { get; set; } = new HashSet<Checkin>();
        //public virtual ICollection<Deadline> Deadlines { get; set; } = new HashSet<Deadline>();
        public virtual ICollection<Grade> Grades { get; set; } = new HashSet<Grade>();
        //public virtual ICollection<Event> Events { get; set; } = new HashSet<Event>();

        // --- Logic & Calculated Properties ---
        [NotMapped] // Prevents EF Core from trying to find a "Gpa" column in the DB
        public double? GPA
        {
            get
            {
                // 1. Get the current numeric score (calculated from assignments)
                var score = CalculatedScore;

                // 2. Logic: If no assignments exist, fall back to the manually entered letter grade
                if (!score.HasValue && !string.IsNullOrEmpty(ManualGrade))
                {
                    return Schedule.ConvertToScale(Schedule.GradePresets.Scales["A"], ManualGrade).GPA;
                }

                // 3. Otherwise, convert the numeric score to a GPA point based on the scale
                return Schedule.ConvertToScale(Schedule.GradePresets.Scales["A"], score).GPA;
            }
        }

        [NotMapped]
        public long LengthInSeconds
        {
            get
            {
                if (!StartTime.HasValue || !EndTime.HasValue) return 0;
                // Normalize to same day to calculate duration
                var duration = EndTime.Value.TimeOfDay - StartTime.Value.TimeOfDay;
                var totalSeconds = (long)duration.TotalSeconds;
                return totalSeconds <= 0 ? totalSeconds + 86400 : totalSeconds;
            }
        }

        [NotMapped]
        public double? CalculatedScore
        {
            get
            {
                var validGrades = Grades.Where(g => g.Percent.HasValue && g.Score.HasValue).ToList();
                if (validGrades.Count == 0) return null;

                double sum = validGrades.Sum(g => g.Percent!.Value * g.Score!.Value);
                double totalPercent = validGrades.Sum(g => g.Percent!.Value);

                return totalPercent > 0 ? Math.Round(sum / totalPercent, 2) : (double?)null;
            }
        }

        [NotMapped]
        public string Color
        {
            get
            {
                // Logic based on index in schedule (requires reference to Schedule.Classes)
                var index = Schedule?.Classes.ToList().IndexOf(this) ?? -1;
                return index switch
                {
                    0 => "#FF0D00",
                    1 => "#FF8900",
                    2 => "#FFD700",
                    3 => "#BAF300",
                    4 => "#2DD700",
                    5 => "#009999",
                    6 => "#162EAE",
                    7 => "#6A0AAB",
                    8 => "#BE008A",
                    _ => "#DDDDDD"
                };
            }
        }
    }
}