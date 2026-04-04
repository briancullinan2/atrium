namespace FlashCard.Pages.Course
{
    public class StudyMetrics : Generators.IGenerator<Card>
    {
        public static IEnumerable<Card> Generate()
        {
            return [
                // Question 1: Checkboxes for tracking hours
                new Card
                {
                    Content = "Which of the following are reasons to track your study hours?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Tracking helps you in several ways, but it does not guarantee that you are prepared. The total number of hours is less important than the quality of those hours spent studying.",
                    Answers =
                    [
                        new Answer { Content = "By studying a certain number of hours per week, you guarantee you will be prepared.", Value = "guarantee" },
                        new Answer { Content = "Tracking your study time helps you avoid procrastination because you can easily see how much time you have been studying.", Value = "procrastination" },
                        new Answer { Content = "Tracking your hours helps cement your new good study habits.", Value = "tracking" },
                        new Answer { Content = "It helps you identify any problems early so you have the time to fix them and they don’t become big problems.", Value = "problems" }
                    ],
                },
                // Question 2: Radio buttons for True/False
                new Card
                {
                    Content = "Your school has many people whose sole job is to make sure you are doing well in school.",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "True - Help provide them some job security and find out what resources are available for you. No need to bang your head against the wall unnecessarily.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "1" },
                        new Answer { Content = "False", Value = "0" }
                    ],
                },
                // Question 3: Text input
                new Card
                {
                    Content = "Why does everyone else look like they have it all together?",
                    ResponseType = CardType.Short,
                    ResponseContent = "They don't. They just typically put on a brave face so that you will think they are smart. Everyone struggles in school, so don't get down on yourself.",
                    Answers =
                    [
                        new Answer { Content = "They don't. They just typically put on a brave face so that you will think they are smart. Everyone struggles in school, so don't get down on yourself.", Value = "1" },
                    ],
                }
            ];
        }
    }
}
