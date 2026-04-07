using FlashData.Entities;
using static FlashData.Entities.UserPack;

namespace FlashData.Extensions;

public static class EntityExtensions
{


    private static readonly int[] Intervals = [1, 2, 4, 7, 14, 28, 84, 168, 364];

    public static Dictionary<int, CardRetention> GetRetention(this List<Response> responses, string? userId, bool refresh = false)
    {

        var result = new Dictionary<int, CardRetention>();
        // get all the cards in all the sets for the responses we're looking at
        var cards = responses.Select(r => r.Card)
            .Distinct().Select(c => c?.Pack)
            .Distinct().SelectMany(r => r?.Cards ?? [])
            .Distinct();

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

        return result;
    }
}
