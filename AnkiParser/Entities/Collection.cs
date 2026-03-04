using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnkiParser.Entities
{

    [Table("col")]
    public class Collection
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; } // Anki uses timestamps as IDs

        [Column("crt")]
        public long CreatedTimestamp { get; set; }

        [Column("mod")]
        public long ModifiedTimestamp { get; set; }

        [Column("scm")]
        public long SchemaModificationTime { get; set; }

        [Column("ver")]
        public int Version { get; set; }

        [Column("dsq")]
        public long DirtySyncSequence { get; set; }

        [Column("usn")]
        public int UpdateSequenceNumber { get; set; }

        [Column("ls")]
        public long LastSyncTime { get; set; }

        [Column("conf")]
        public string Configuration { get; set; } = string.Empty; // JSON blob

        [Column("models")]
        public string NoteTypes { get; set; } = string.Empty; // JSON blob (Templates)

        [Column("decks")]
        public string Decks { get; set; } = string.Empty; // JSON blob

        [Column("dconf")]
        public string DeckConfig { get; set; } = string.Empty; // JSON blob

        [Column("tags")]
        public string Tags { get; set; } = string.Empty; // JSON cache
    }
}
