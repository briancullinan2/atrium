


namespace FlashData.Entities;

[Table("course")]
public class Course : Entity<Course>, IHasUser, IHasLogo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; protected set; }
    // Logo Relationship
    public int? LogoId { get; set; }
    public virtual string? LogoHosted { get; set; }

    [Required]
    [MaxLength(200)]
    [Category("Content")]
    [Display(Name = "Course Title", Description = "The name of the educational course (e.g., Arizona Real Estate License)")]
    public string Title { get; set; } = string.Empty;

    [Category("Content")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Description", Description = "A brief overview of what the course covers")]
    public string Description { get; set; } = string.Empty;

    [Category("Content")]
    [Display(Name = "Category", Description = "Industry or subject area (e.g., Real Estate, Medical, Law)")]
    public string? Category { get; set; }
    [Display(Name = "Subject", GroupName = "Pack Info", Description = "Formal school subject the pack falls under", Order = 1)]
    public string Subject { get; set; } = "";

    [Category("Economics")]
    [Display(Name = "Bundled Price", Description = "Discounted price for purchasing the entire course instead of individual packs")]
    public decimal? Price { get; set; }

    [Required]
    [Category("Status")]
    public bool IsPublished { get; set; } = false;

    [Category("System")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    // Relationship: A Course has many Packs
    [Category("Structure")]
    [Display(Name = "Study Packs", Description = "The individual modules/packs that make up this course")]
    public virtual ICollection<Pack> Packs { get; set; } = new HashSet<Pack>();

    // Optional: Track who created the course (similar to your Pack entity)
    [Category("Ownership")]
    public string? UserId { get; set; }


    [Category("Structure")]
    [Display(Name = "Parent Course", Description = "The bigger course this course belongs to")]
    public int? CourseId { get; set; }

    [ForeignKey(nameof(CourseId))]
    public virtual Course? Parent { get; set; }
    [InverseProperty(nameof(Parent))]
    public virtual ICollection<Course> Courses { get; set; } = new HashSet<Course>();
    [InverseProperty(nameof(Lesson.Course))]
    public virtual ICollection<Lesson> Lessons { get; set; } = new HashSet<Lesson>();

    //[InverseProperty(nameof(User.Courses))]
    //public virtual ICollection<User> Users { get; set; } = new HashSet<User>();
    //[InverseProperty(nameof(Group.Courses))]
    //public virtual ICollection<Group> Groups { get; set; } = new HashSet<Group>();


}