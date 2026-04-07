
namespace FlashData.Entities;

[Table("message")]
public class Message : Entity<Message>
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long MessageId { get; set; } // Matches EPIC's BigInt

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4096)]
    public string Body { get; set; } = string.Empty;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    // From Clinical: Scheduling/Visibility window
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    // From StudySauce: Track environment (Dev/Prod) and Status (Read/Unread/Sent)
    public int Status { get; set; }
    public string? Environment { get; set; }

    public int MessageType { get; set; }
    public string? Source { get; set; }

    // Logic helper: Is the message currently valid for display?
    [NotMapped]
    public bool IsVisible => IsActive &&
                             DateTime.UtcNow >= StartTime &&
                             (EndTime == null || DateTime.UtcNow <= EndTime);

    public Message()
    {
        Created = DateTime.UtcNow;
        StartTime = DateTime.UtcNow;
    }
}