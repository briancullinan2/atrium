namespace FlashData.Entities;

[Table("card")]
public class Card : Entity<Card>
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; protected set; }

    public int? PackId { get; set; }

    [ForeignKey(nameof(PackId))]
    public virtual Pack? Pack { get; set; }

    [Category("Content")]
    [Display(Name = "Question/Front", Description = "The primary content displayed to the user")]
    [DataType(DataType.MultilineText)]
    public string Content { get; set; } = string.Empty;

    [Category("Content")]
    [Display(Name = "Answer/Back", Description = "The expected response or back of the flashcard")]
    [DataType(DataType.MultilineText)]
    public string? ResponseContent { get; set; } = string.Empty;

    [MaxLength(16)]
    [Category("Settings")]
    [Display(Name = "Content Type", Description = "Format of the front (TEXT, IMAGE, etc.)")]
    public DisplayType ContentType { get; set; } = DisplayType.Text;

    [MaxLength(16)]
    [Category("Settings")]
    [Display(Name = "Response Type", Description = "Interaction type (flash-card, multiple-choice)")]
    public CardType ResponseType { get; set; } = CardType.FlashCard;

    [MaxLength(16)]
    [Category("Scheduling")]
    [Display(Name = "Recurrence", Description = "Spaced repetition interval")]
    public string Recurrence { get; set; } = "1 day";

    [Required]
    [Category("System")]
    public bool QuizOnly { get; set; } = false;

    [Required]
    [Category("System")]
    public bool Excluded { get; set; } = false;
    [Required]
    [Category("System")]
    public bool Deleted { get; set; } = false;

    [Required]
    [Category("System")]
    public DateTime Created { get; protected set; } = DateTime.UtcNow;

    [Category("System")]
    public DateTime? Modified { get; set; }


    public string? Tag { get; set; }
    public string? Source { get; set; }

    // Navigation for Anki-style responses and multiple choice answers
    //public virtual ICollection<Response> Responses { get; set; } = new HashSet<Response>();
    [Category("Answers")]
    [InverseProperty(nameof(Answer.Card))]
    public virtual ICollection<Answer> Answers { get; set; } = new HashSet<Answer>();

    public Card()
    {
        Created = DateTime.Now;
    }
}