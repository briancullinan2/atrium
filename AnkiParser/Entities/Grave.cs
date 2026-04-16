using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnkiParser.Entities;

[Table("graves")]
public class Grave
{
    // The grave table in Anki doesn't have its own ID, 
    // it uses the original ID of the deleted item.
    [Key, Column("oid")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long OriginalId { get; set; }

    [Column("type")]
    public int Type { get; set; } // 0=Card, 1=Note, 2=Deck

    [Column("usn")]
    public int UpdateSequenceNumber { get; set; }
}
