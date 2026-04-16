namespace FlashCard.Pages.Course;

public class SpacedRepetition : IGenerator<Card>
{
    public static IEnumerable<Card> Generate()
    {
        return [
            // Question 1: Radio buttons for Spacing importance
            new Card
            {
                Content = "Spacing out your study sessions isn't important at all.",
                ResponseType = CardType.TrueFalse,
                ResponseContent = "False. We just can't resist beating this dead horse.",
                Answers =
                [
                    new Answer { Content = "True", Value = "True" },
                    new Answer { Content = "False", Value = "False" }
                ]
            },

            // Question 2: Textarea for The Forgetting Curve
            new Card
            {
                Content = "What is the Forgetting Curve?",
                ResponseType = CardType.Short,
                ResponseContent = "The forgetting curve illustrates how quickly we lose information from our memory if we do not return to reinforce it. We forget almost everything we learn in a manner of days.",
            },

            // Question 3: Radio buttons for Revisiting frequency
            new Card
            {
                Content = "How often do we recommend you revisit your flash cards in spaced repetition study sessions?",
                ResponseType = CardType.Multiple,
                ResponseContent = "We recommend going through the same material once a week for the first month. After that, revisit as necessary.",
                Answers =
                [
                    new Answer { Content = "Every day", Value = "daily" },
                    new Answer { Content = "Once a week", Value = "weekly" },
                    new Answer { Content = "Every other week", Value = "biweekly" },
                    new Answer { Content = "Once a month", Value = "monthly" }
                ]
            },

            // Question 4: Radio buttons for Naming
            new Card
            {
                Content = "Which of the following is NOT another name for Spaced Repetition?",
                ResponseType = CardType.Multiple,
                ResponseContent = "Spaced Repetition is also know as spaced practice and distributed practice.",
                Answers =
                [
                    new Answer { Content = "Spaced practice", Value = "practice" },
                    new Answer { Content = "Distributed practice", Value = "distributed" },
                    new Answer { Content = "Blocked practice", Value = "blocked" }
                ]
            }
        ];
    }
}
