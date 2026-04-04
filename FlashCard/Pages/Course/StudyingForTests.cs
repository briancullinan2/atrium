namespace FlashCard.Pages.Course
{
    public class StudyingForTests : Generators.IGenerator<Card>
    {
        public static IEnumerable<Card> Generate()
        {
            return [
                // Question 1: Checkboxes for Objective Tests
                new Card
                {
                    Content = "Which of the following types of tests are objective?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Math & Science, multiple choice, and true/false tests are all objective - meaning they have a definite right answer.",
                    Answers =
                    [
                        new Answer { Content = "Essay", Value = "essay" },
                        new Answer { Content = "Short Answer", Value = "short" },
                        new Answer { Content = "Math & Science", Value = "math", IsCorrect = true },
                        new Answer { Content = "Multiple Choice", Value = "multiple", IsCorrect = true },
                        new Answer { Content = "True False", Value = "true", IsCorrect = true }
                    ],
                },

                // Question 2: Text input for Most Important Thing
                new Card
                {
                    Content = "What is the most important thing in studying for tests?",
                    ResponseType = CardType.Short,
                    Answers =
                    [
                        new Answer { Content = "Space out your studying!", Value = "Space out your studying" }
                    ],
                },

                // Question 3: Multiple Text inputs for Open Note Tips
                new Card
                {
                    Content = "What are two tips for open notes tests?",
                    ResponseType = CardType.Short, // Custom type for multiple text boxes
                    ResponseContent = "First, you still have to study. In fact, you may need to study more. Second, spending a little extra time organizing your notes is well worth the effort. You need to be able to get to the most important information immediately during the test.",
                    Answers =
                    [
                        new Answer { Content = "Tip 1", Value = "tip1" },
                        new Answer { Content = "Tip 2", Value = "tip2" }
                    ],
                }
            ];
        }
    }
}
