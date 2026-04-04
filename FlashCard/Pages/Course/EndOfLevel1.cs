namespace FlashCard.Pages.Course
{
    public class EndOfLevel1 : Generators.IGenerator<Card>
    {
        public static IEnumerable<Card> Generate()
        {
            return [
                // Survey Question: Course Enjoyment
                new Card
                {
                    Content = "Have you enjoyed the Atrium course?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "We hope you have enjoyed the course to this point.  Even if you decide not to upgrade, you are welcome to continue to use our free tools.",
                    QuizOnly = true,
                    Answers =
                    [
                        new Answer {
                            Content = "Yes",
                            Value = "1",
                            IsCorrect = true
                        },
                        new Answer {
                            Content = "No",
                            Value = "0",
                            IsCorrect = true
                        }
                    ]
                }
            ];
        }
    }
}
