using DataLayer.Customization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.Entities
{
    [Table("schedule")]
    public class Schedule : Entity<Schedule>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public string? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; } = null!;

        [MaxLength(256)]
        [Column("university")]
        public string? University { get; set; }

        [MaxLength(10)]
        [Column("grades")]
        public string? Grades { get; set; }

        [MaxLength(10)]
        [Column("weekends")]
        public string? Weekends { get; set; }

        [Column("sharp6am11am")]
        public int? Sharp6am11am { get; set; }

        [Column("sharp11am4pm")]
        public int? Sharp11am4pm { get; set; }

        [Column("sharp4pm9pm")]
        public int? Sharp4pm9pm { get; set; }

        [Column("sharp9pm2am")]
        public int? Sharp9pm2am { get; set; }

        /// <summary>
        /// Doctrine 'array' for alerts. Best mapped to JSON string in modern DBs.
        /// </summary>
        [Column("alerts", TypeName = "json")]
        public string? Alerts { get; set; }

        [Column("grade_scale_json", TypeName = "json")]
        public string? GradeScaleJson { get; set; } = "";
        /// <summary>
        /// Doctrine 'json_array' for grade scale.
        /// </summary>
        [Column("grade_scale")]
        public GradeScale GradeScale { get; set; } = GradeScale.Unset;

        [Required]
        [Column("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [Column("term")]
        public DateTime? Term { get; set; }

        // Navigation Properties
        public virtual ICollection<Subject> Courses { get; set; } = new HashSet<Subject>();
        //public virtual ICollection<Event> Events { get; set; } = new HashSet<Event>();

        // --- Logic Helper Properties (Equivalent to your PHP methods) ---

        [NotMapped]
        public IEnumerable<Subject> Classes => Courses?.Where(c => !c.Deleted && c.Type == "c") ?? [];

        [NotMapped]
        public IEnumerable<Subject> Others => Courses?.Where(c => !c.Deleted && c.Type == "o") ?? [];

        public double GetCreditHours() => Courses.Sum(c => c.CreditHours ?? 0.0);

        public double? GetGPA()
        {
            var hours = GetCreditHours();
            if (hours == 0 || !Classes.Any(c => c.GPA.HasValue))
                return null;

            var totalPoints = Classes.Sum(c => (c.GPA ?? 0.0) * (c.CreditHours ?? 0.0));
            return Math.Round(totalPoints / hours, 2);
        }

        public Schedule()
        {
            Created = DateTime.UtcNow;
        }

        public class ScaleEntry
        {
            public string? Letter { get; set; }
            public int Max { get; set; }
            public int Min { get; set; }
            public double Point { get; set; }
        }

        public static class GradePresets
        {
            public static readonly Dictionary<string, List<ScaleEntry>> Scales = new()
            {
                ["A +/-"] =
                [
                    new() { Letter = "A+", Max = 100, Min = 97, Point = 4.0 },
                    new() { Letter = "A",  Max = 96,  Min = 93, Point = 4.0 },
                    new() { Letter = "A-", Max = 92,  Min = 90, Point = 3.7 },
                    new() { Letter = "B+", Max = 89,  Min = 87, Point = 3.3 },
                    new() { Letter = "B",  Max = 86,  Min = 83, Point = 3.0 },
                    new() { Letter = "B-", Max = 82,  Min = 80, Point = 2.7 },
                    new() { Letter = "C+", Max = 79,  Min = 77, Point = 2.3 },
                    new() { Letter = "C",  Max = 76,  Min = 73, Point = 2.0 },
                    new() { Letter = "C-", Max = 72,  Min = 70, Point = 1.7 },
                    new() { Letter = "D+", Max = 69,  Min = 67, Point = 1.3 },
                    new() { Letter = "D",  Max = 66,  Min = 63, Point = 1.0 },
                    new() { Letter = "D-", Max = 62,  Min = 60, Point = 0.7 },
                    new() { Letter = "F",  Max = 59,  Min = 0,  Point = 0.0 }
                ],
                ["A"] =
                [
                    new() { Letter = "A", Max = 100, Min = 90, Point = 4.0 },
                    new() { Letter = "B", Max = 89,  Min = 80, Point = 3.0 },
                    new() { Letter = "C", Max = 79,  Min = 70, Point = 2.0 },
                    new() { Letter = "D", Max = 69,  Min = 60, Point = 1.0 },
                    new() { Letter = "F", Max = 59,  Min = 0,  Point = 0.0 }
                ]
            };
        }

        public static (string? Letter, double? GPA) ConvertToScale(GradeScale? scale, object? score)
        {
            return ConvertToScale(scale == GradeScale.AthroughFPlusMinus ? Schedule.GradePresets.Scales["A +/-"] : Schedule.GradePresets.Scales["A"], score);
        }

        public static (string? Letter, double? GPA) ConvertToScale(List<ScaleEntry>? scale, object? score)
        {
            if (score == null) return (null, null);

            // Fallback to default scale if provided scale is invalid
            var activeScale = (scale == null || scale.Count == 0) ? GradePresets.Scales["A"] : scale;

            return score switch
            {
                // Case 1: Input is a letter grade (e.g., "B+")
                string letter => activeScale
                    .Where(s => s.Letter?.Equals(letter, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(s => (s.Letter, (double?)s.Point))
                    .FirstOrDefault((null, null)),

                // Case 2: Input is a numeric score (e.g., 88.5)
                double numScore => activeScale
                    .Where(s => Math.Round(numScore) <= s.Max && Math.Round(numScore) >= s.Min)
                    .Select(s => (s.Letter, (double?)s.Point))
                    .FirstOrDefault((null, null)),

                // Fallback for unexpected types
                _ => (null, null)
            };
        }
    }
}