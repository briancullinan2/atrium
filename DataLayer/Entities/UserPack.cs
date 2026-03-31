using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DataLayer.Entities
{
    [Table("userPack")]
    [PrimaryKey(nameof(UserId), nameof(PackId))]
    public class UserPack : Entity<UserPack>
    {
        // Composite Key Properties
        public string? UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

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
        public IEnumerable<Response>? GetResponses(out int correctCount)
        {
            var responses = User?.Responses.Where(r => r.Card?.PackId == PackId);
            correctCount = responses?.Count(r => r.IsCorrect) ?? 0;
            return responses;
        }

        [NotMapped]
        private static readonly int[] Intervals = [1, 2, 4, 7, 14, 28, 84, 168, 364];

        public record CardRetention(
            int Interval,
            DateTime? LastIntervalDate,
            bool IsDue,
            DateTime? LastResponseDate
        );

        public Dictionary<int, CardRetention> GetRetention(bool refresh = false)
        {
            // 1. Return cached version if available and not refreshing
            if (!string.IsNullOrEmpty(RetentionJson) && !refresh
                && JsonSerializer.Deserialize<Dictionary<int, CardRetention>>(RetentionJson) is Dictionary<int, CardRetention> existing
                && existing.Count == Pack?.Cards.Count(c => !c.Deleted))
            {
                return existing;
            }

            var result = new Dictionary<int, CardRetention>();
            var cards = Pack?.Cards.Where(c => !c.Deleted) ?? [];
            var responses = User?.Responses
                .Where(r => r.Card?.PackId == PackId)
                .OrderBy(r => r.Created)
                .ToList() ?? [];

            foreach (var card in cards)
            {
                var cardResponses = responses.Where(r => r.CardId == card.Id);
                DateTime? lastIntervalDate = null;
                DateTime? lastResponseDate = null;
                int intervalIndex = 0;
                bool correctAfter = false;

                foreach (var r in cardResponses)
                {
                    lastResponseDate = r.Created;

                    if (r.IsCorrect)
                    {
                        // The "3 AM" rule logic: normalize dates to 3 AM to ignore 
                        // responses that happen too close together in a single 'day'
                        while (intervalIndex < Intervals.Length)
                        {
                            var nextDueThreshold = lastIntervalDate?.AddDays(Intervals[intervalIndex]) ?? DateTime.MinValue;

                            // Reset to 3 AM for comparison
                            var normalizedResponse = r.Created.Date.AddHours(3);
                            var normalizedThreshold = nextDueThreshold.Date.AddHours(3);

                            if (lastIntervalDate == null || normalizedResponse >= normalizedThreshold)
                            {
                                lastIntervalDate = r.Created;
                                intervalIndex++;
                            }
                            else break;
                        }
                        correctAfter = true;
                    }
                    else
                    {
                        // Reset on wrong answer
                        intervalIndex = 0;
                        lastIntervalDate = r.Created;
                        correctAfter = false;
                    }
                }

                // Clamp index
                intervalIndex = Math.Clamp(intervalIndex, 0, Intervals.Length - 1);
                int currentInterval = Intervals[intervalIndex];

                // Determine if due: never answered, or interval elapsed, or failed last time
                bool isDue = lastIntervalDate == null ||
                             (intervalIndex == 0 && !correctAfter) ||
                             lastIntervalDate.Value.Date.AddHours(3).AddDays(currentInterval) <= DateTime.Now.Date.AddHours(3);

                result[(int)card.Id] = new CardRetention(
                    currentInterval,
                    lastIntervalDate,
                    isDue,
                    lastResponseDate
                );
            }

            // Persist to the JSON column
            RetentionJson = JsonSerializer.Serialize(result);
            return result;
        }
    }
}