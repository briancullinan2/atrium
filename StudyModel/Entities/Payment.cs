
namespace StudyModel.Entities
{
    [Table("payment")]
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        public int? PackId { get; set; }

        [ForeignKey(nameof(PackId))]
        public virtual Pack? Pack { get; set; }

        public int? CourseId { get; set; }

        [ForeignKey(nameof(CourseId))]
        public virtual Course? Course { get; set; }

        [Required]
        [MaxLength(12)]
        public string Amount { get; set; } = "0.00";
        // Note: Consider using decimal for calculations, string for DB storage compatibility

        [Required]
        [MaxLength(256)]
        public string First { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Last { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? PaymentReference { get; set; } // Renamed from 'payment' to avoid class name conflict

        [MaxLength(256)]
        public string? Subscription { get; set; }

        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [Required]
        public bool Deleted { get; set; } = false;

        /// <summary>
        /// Many-to-Many relationship with Coupons.
        /// EF Core will automatically manage the 'payment_coupon' join table.
        /// </summary>
        //public virtual ICollection<Coupon> Coupons { get; set; }

        public Payment()
        {
            //Coupons = new HashSet<Coupon>();
            Created = DateTime.UtcNow;
        }
    }
}