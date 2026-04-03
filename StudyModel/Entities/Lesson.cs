using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudyModel.Entities
{
    [Table("lesson")]
    public class Lesson : Entity<Lesson>
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Icon { get; set; } = "bi-circle";

        // Links back to the Type-based logic you have
        // Store as a string (e.g., "SettingGoals") to map back to your 15 classes
        public string? Tag { get; set; } = string.Empty;

        public int Level { get; set; }
        public int? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public virtual Lesson? Parent { get; set; }
        public virtual ICollection<Lesson> Children { get; set; } = [];

        public int? CourseId { get; set; }
        [ForeignKey(nameof(CourseId))]
        public Course? Course { get; set; }


        // Content Encapsulation
        public string IntroductionHtml { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public int? QuizPackId { get; set; } // Foreign key to your Card/Pack entities

        // Reward Information
        public string RewardTitle { get; set; } = string.Empty;
        public string RewardImageUrl { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = string.Empty; // e.g., "setup-hours"
        public string RewardDescription { get; set; } = string.Empty;

        // Investment/Next Step Instructions
        public string InvestmentActionText { get; set; } = string.Empty;
        public string InvestmentActionHref { get; set; } = string.Empty;

        public bool IsBeta { get; set; }
        public string? RoleRequired { get; set; }
    }
}