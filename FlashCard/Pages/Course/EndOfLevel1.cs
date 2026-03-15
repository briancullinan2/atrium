using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class EndOfLevel1 : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                // Survey Question: Course Enjoyment
                new Card
                {
                    Content = "Have you enjoyed the Atrium course?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "We hope you have enjoyed the course to this point.  Even if you decide not to upgrade, you are welcome to continue to use our free tools.",
                    QuizOnly = true,
                    Answers = new List<Answer>
                    {
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
                    }
                }
            ];
        }
    }
}
