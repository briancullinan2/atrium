using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnkiParser.Entities;

[Table("notes")]
public class Note
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; } // Timestamp in ms

    [Required]
    [Column("guid")]
    public string Guid { get; set; } = string.Empty; // Global unique ID for syncing

    [Column("mid")]
    public long ModelId { get; set; } // Foreign key to the 'models' in 'col'

    [Column("mod")]
    public long ModifiedTimestamp { get; set; }

    [Column("usn")]
    public int UpdateSequenceNumber { get; set; }

    [Column("tags")]
    public string Tags { get; set; } = string.Empty; // Space-separated tags

    [Column("flds")]
    public string Fields { get; set; } = string.Empty; // Data separated by \x1f

    [Column("sfld")]
    public string SortField { get; set; } = string.Empty; // Used for sorting in browser

    [Column("csum")]
    public long Checksum { get; set; } // Used for duplicate detection

    [Column("flags")]
    public int Flags { get; set; }

    [Column("data")]
    public string Data { get; set; } = string.Empty; // Unused in modern Anki

    // Navigation property to Cards (1 Note -> Many Cards)
    [InverseProperty(nameof(Card.Note))]
    public virtual ICollection<Card> Cards { get; set; } = new HashSet<Card>();

    // Helper property to parse the fields
    [NotMapped]
    public string[] FieldList => Fields.Split('\x1f');
}
