


namespace FlashData.Entities;

[Table("pack")]
public class Pack : Entity<Pack>, IHasGroup, IHasUser, IHasLogo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Category("Ownership")]
    [Display(Name = "Owner Group", Description = "The primary group owning this pack")]
    public int? GroupId { get; set; }

    [Category("Structure")]
    [Display(Name = "Parent Course", Description = "The course this pack belongs to")]
    public int? CourseId { get; set; }

    [ForeignKey(nameof(CourseId))]
    public virtual Course? Course { get; set; }

    [Category("Ownership")]
    [Display(Name = "Created By", Description = "The user who created this pack")]
    public string? UserId { get; set; }

    // Logo Relationship
    public int? LogoId { get; set; }
    public virtual string? LogoHosted { get; set; }

    [Required]
    [Category("Content")]
    [Display(Name = "Title", GroupName = "Title", Description = "The display title of the card pack", Order = 0)]
    public string Title { get; set; } = string.Empty;

    [Category("Content")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Description", GroupName = "Pack Info", Description = "Detailed description of the pack's content", Order = 1)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0, 999999.99)]
    [Category("Economics")]
    [DataType(DataType.Currency)]
    [Display(Name = "Price", GroupName = "Pack Status", Description = "Set the purchase price for this pack")]
    public decimal Price { get; set; } = 0;

    [Required]
    [Range(0, 999999)]
    [Category("Economics")]
    [DataType(DataType.Currency)]
    [Display(Name = "Tokens", GroupName = "Pack Status", Description = "Set the purchase tokens for this pack")]
    public int Tokens { get; set; } = 0;

    [Required]
    [MaxLength(16)]
    [Category("Status")]
    [Display(Name = "Status", GroupName = "Pack Status", Description = "Current publication state")]
    public PackStatus Status { get; set; } = PackStatus.Unpublished;

    [Category("Stats")]
    [Display(Name = "Downloads", Description = "Total number of times this pack was acquired")]
    public int Downloads { get; set; } = 0;

    [Range(0, 5)]
    [Category("Stats")]
    [Display(Name = "Rating")]
    public decimal Rating { get; set; } = 0;

    [Category("Scheduling")]
    [Display(Name = "Active From", GroupName = "Pack Status", Description = "Date when pack becomes available")]
    public DateTime? ActiveFrom { get; set; }

    [Category("Scheduling")]
    [Display(Name = "Active To", GroupName = "Pack Status", Description = "Date when pack expires")]
    public DateTime? ActiveTo { get; set; }

    [Required]
    [Category("System")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [Category("System")]
    public DateTime? Modified { get; set; }

    // Mapped to Doctrine's simple_array or array types
    [Display(Name = "Tag", GroupName = "Pack Info", Description = "Personal descriptor to track pack", Order = 1)]
    public string Tag { get; set; } = "";
    [Display(Name = "Tags", GroupName = "Pack Info", Description = "Descriptor tags of pack content", Order = 1)]
    public string Tags { get; set; } = "";
    [Display(Name = "Category", GroupName = "Pack Info", Description = "Classification of deck content", Order = 1)]
    public string Category { get; set; } = "";
    [Display(Name = "Subject", GroupName = "Pack Info", Description = "Formal school subject the pack falls under", Order = 1)]
    public string Subject { get; set; } = "";


    // Navigation Collections (Doctrine ManyToMany/OneToMany)
    //public virtual ICollection<Group> Groups { get; set; } = new HashSet<Group>();
    //public virtual ICollection<Coupon> Coupons { get; set; } = new HashSet<Coupon>();
    public virtual ICollection<UserPack> UserPacks { get; set; } = new HashSet<UserPack>();
    [Category("Content")]
    [Display(Name = "Cards", Description = "Set of card content to display to users")]
    public virtual ICollection<Card> Cards { get; set; } = new HashSet<Card>();
}