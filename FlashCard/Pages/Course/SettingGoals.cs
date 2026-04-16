namespace FlashCard.Pages.Course;

public class SettingGoals : IGenerator<Card>
{
    public static IEnumerable<Card> Generate()
    {
        return [
            new Card
            {
                Content = "How much more likely are you to perform at a higher level if you set specific and challenging goals?",
                ResponseType = CardType.Multiple,
                ResponseContent = "You are 90% more likely to perform at a higher level if you set specific and challenging goals.",
                Answers =
                [
                    new Answer { Content = "20%", Value = "20", IsCorrect = false },
                    new Answer { Content = "40%", Value = "40", IsCorrect = false },
                    new Answer { Content = "60%", Value = "60", IsCorrect = false },
                    new Answer { Content = "90%", Value = "90", IsCorrect = true }
                ]
            },
            new Card
            {
                Content = "What does the SMART acronym stand for?",
                ResponseType = CardType.Short,
                ResponseContent = "Answers are specific, measurable, achievable, relevant, time-bound",
                Answers =
                [
                    new Answer { Content = "Specific", IsCorrect = true, Value = "quiz-specific" },
                    new Answer { Content = "Measurable", IsCorrect = true, Value = "quiz-measurable" },
                    new Answer { Content = "Achievable", IsCorrect = true, Value = "quiz-achievable" },
                    new Answer { Content = "Relevant", IsCorrect = true, Value = "quiz-relevant" },
                    new Answer { Content = "Time-bound", IsCorrect = true, Value = "quiz-timeBound" }
                ]
            },
            new Card
            {
                Content = "What are the two types of motivation?",
                ResponseType = CardType.Short,
                ResponseContent = "The two types of motivation are intrinsic and extrinsic motivation.\nIntrinsic motivation is motivation that comes from within.  Ex. studying because you want the satisfaction of learning something new.\nExtrinsic motivation is a reward that comes externally.  Ex. studying in order to get a good grade.",
                Answers =
                [
                    new Answer { Content = "Intrinsic", IsCorrect = true, Value = "quiz-intrinsic" },
                    new Answer { Content = "Extrinsic", IsCorrect = true, Value = "quiz-extrinsic" }
                ]
            }
        ];
    }
}