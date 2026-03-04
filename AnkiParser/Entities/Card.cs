using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnkiParser.Entities
{
    [Table("cards")]
    public class Card
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; } // Timestamp in ms

        [Column("nid")]
        public long NoteId { get; set; } // Foreign Key to Note
        [ForeignKey("nid")]
        public Note? Note { get; set; }

        [Column("did")]
        public long DeckId { get; set; } // Foreign Key to Deck in 'col'

        [Column("ord")]
        public int Ordinal { get; set; } // Which template index (0 = Card 1, 1 = Card 2)

        [Column("mod")]
        public long ModifiedTimestamp { get; set; }

        [Column("usn")]
        public int UpdateSequenceNumber { get; set; }

        [Column("type")]
        public int Type { get; set; } // 0=new, 1=learning, 2=review, 3=relearning

        [Column("queue")]
        public int Queue { get; set; } // -1=suspended, 0=new, 2=review

        [Column("due")]
        public long Due { get; set; } // Day number or Timestamp

        [Column("ivl")]
        public int Interval { get; set; } // Days between reviews

        [Column("factor")]
        public int Factor { get; set; } // Ease factor (e.g., 2500 = 250%)

        [Column("reps")]
        public int Repetitions { get; set; } // Number of times reviewed

        [Column("lapses")]
        public int Lapses { get; set; } // Number of times forgotten

        [Column("left")]
        public int Left { get; set; } // Remaining steps in learning

        [Column("odue")]
        public long OriginalDue { get; set; } // For "filtered" decks

        [Column("odid")]
        public long OriginalDeckId { get; set; } // For "filtered" decks

        [Column("flags")]
        public int Flags { get; set; } // User-defined flags (1=Red, 2=Orange, etc.)

        [Column("data")]
        public string Data { get; set; } = string.Empty; // Unused placeholder
    }
}
