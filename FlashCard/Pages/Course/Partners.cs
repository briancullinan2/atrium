using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class Partners : DataLayer.Generators.IGenerator<Card>
    {
        public static IEnumerable<Card> Generate()
        {

            return [
                // Question 1: How accountability partners help
                new Card
                {
                    Content = "Select the two main ways an accountability partner can help you in school from the list below:",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Your accountability partner may be able to help you in several different ways, but the two main ways are by keeping you focused and motivated.",
                    Answers =
                    [
                        new Answer { Content = "To motivate you", Value = "motivate", IsCorrect = true },
                        new Answer { Content = "Tutoring for your most difficult classes", Value = "tutor", IsCorrect = false },
                        new Answer { Content = "Help keep you focused", Value = "focus", IsCorrect = true },
                        new Answer { Content = "To incentivize you to achieve your goals", Value = "incentive", IsCorrect = false }
                    ]
                },

                // Question 2: Key attributes
                new Card
                {
                    Content = "Which of the following is not a key attribute to look for when choosing your accountability partner?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Take time to choose your accountability partner.  You should have already established trust with the person because you will need them to challenge you and to celebrate successes with you.",
                    Answers =
                    [
                        new Answer { Content = "Someone you trust.", Value = "trust", IsCorrect = false },
                        new Answer { Content = "Someone that will challenge you.", Value = "challenge", IsCorrect = false },
                        new Answer { Content = "Someone that knows you best.", Value = "knows", IsCorrect = true },
                        new Answer { Content = "Someone that will celebrate your successes.", Value = "celebrate", IsCorrect = false }
                    ]
                },

                // Question 3: Frequency of communication
                new Card
                {
                    Content = "How often should you talk with your accountability partner?",
                    ResponseType = CardType.Short,
                    ResponseContent = "Ideally, you can communicate with your accountability partner on a weekly basis.",
                    Answers =
                    [
                        new Answer { Content = "Ideally, you can communicate with your accountability partner on a weekly basis.", Value = "weekly", IsCorrect = true }
                    ]
                },

                // Question 4: Other examples of use
                new Card
                {
                    Content = "According to the video, which of the following are examples of other ways accountability partners are used?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Although there are many other ways that accountability partners are used, the video specifically highlights gyms, dieting, and churches.",
                    Answers =
                    [
                        new Answer { Content = "Learning to drive", Value = "drive", IsCorrect = false },
                        new Answer { Content = "Dieting", Value = "dieting", IsCorrect = true },
                        new Answer { Content = "Gyms", Value = "gyms", IsCorrect = true },
                        new Answer { Content = "Churches", Value = "churches", IsCorrect = true }
                    ]
                }
            ];
        }
    }
}
