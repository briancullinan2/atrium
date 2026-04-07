using System.Text.Json;

namespace FlashData.Entities;

[Table("userPack")]
[PrimaryKey(nameof(UserId), nameof(PackId))]
public class UserPack : Entity<UserPack>, IHasUser
{
    // Composite Key Properties
    public string? UserId { get; set; }

    public int? PackId { get; set; }
    [ForeignKey(nameof(PackId))]
    public virtual Pack? Pack { get; set; }

    // Columns
    public decimal Priority { get; set; } = 0;

    public DateTime? RetryFrom { get; set; }
    public DateTime? RetryTo { get; set; }
    public DateTime? Downloaded { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    // EF Core doesn't support 'array' type natively like Doctrine. 
    // We store it as a JSON string or a serialized blob.
    public string? RetentionJson { get; set; }

    public bool Removed { get; set; } = false;

    // Business Logic ported from PHP
    /*public IEnumerable<Response>? GetResponses(out int correctCount)
    {
        var responses = .Responses.Where(r => r.Card?.PackId == PackId);
        correctCount = responses?.Count(r => r.IsCorrect) ?? 0;
        return responses;
    }
    */

    public record CardRetention(
        int Interval,
        DateTime? LastIntervalDate,
        bool IsDue,
        DateTime? LastResponseDate
    );

}