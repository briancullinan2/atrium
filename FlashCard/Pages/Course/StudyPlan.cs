namespace FlashCard.Pages.Course;

public class StudyPlan : Generators.IGenerator<Card>
{
    public static IEnumerable<Card> Generate()
    {
        return [
            // Question 1: Radio buttons for the multiplier
            new Card
            {
                Content = "Multiply your class hours by ___ to get the total number of hours you should study per week.",
                ResponseType = CardType.Multiple,
                ResponseContent = "Multiply by 3 to get the number of weekly study hours.",
                Answers =
                [
                    new Answer { Content = "1", Value = "1" },
                    new Answer { Content = "2", Value = "2" },
                    new Answer { Content = "3", Value = "3" },
                    new Answer { Content = "4", Value = "4" }
                ],
            },
            // Question 2: Procrastination text area
            new Card
            {
                Content = "How does building a study plan help you stop procrastinating?",
                ResponseType = CardType.Short,
                ResponseContent = "You commit yourself to studying at a time when you are motivated instead of waiting until you can find reasons not to study."
            },
            // Question 3: Study Sessions text area
            new Card
            {
                Content = "How should you use the time in your free study sessions?",
                ResponseType = CardType.Short,
                ResponseContent = "This time should be allocated to give you the flexibility to focus on whichever class is most in need of attention at the time. It is also a great time to work on bigger projects or papers that span several weeks."
            },
            // Question 4: Sticking to the plan text area
            new Card
            {
                Content = "What are two methods you can use to help yourself stick with a study plan?",
                ResponseType = CardType.Short,
                ResponseContent = "Starting immediately is a great way to get some momentum. Additionally, choosing study locations ahead of time can help you keep your motivation."
            }
        ];
    }
}
