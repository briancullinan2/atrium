using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnkiParser.Entities
{
    [Table("revlog")]
    public class Review
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; } // Timestamp in ms

        [Column("cid")]
        public long CardId { get; set; } // Foreign Key to Card

        [Column("usn")]
        public int UpdateSequenceNumber { get; set; }

        [Column("ease")]
        public int Ease { get; set; } // 1=Again, 2=Hard, 3=Good, 4=Easy

        [Column("ivl")]
        public int Interval { get; set; } // New interval after review

        [Column("lastIvl")]
        public int LastInterval { get; set; } // Interval before review

        [Column("factor")]
        public int Factor { get; set; } // New factor after review

        [Column("time")]
        public int TimeTaken { get; set; } // Time in ms spent answering

        [Column("type")]
        public int Type { get; set; } // Review, Learn, etc.
    }
}
