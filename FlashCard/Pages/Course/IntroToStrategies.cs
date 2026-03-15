using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class IntroToStrategies : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                new Card
                {
                    Content = "Which of the following are examples of self-testing?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Creating and using flash cards, teaching others concepts, and creating practice tests are all great examples of self-testing. Reading and rereading notes is a passive form of studying that is far less effective.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Reading and rereading your notes", Value = "reading" },
                        new Answer { Content = "Flash cards", Value = "flash" },
                        new Answer { Content = "Teaching others", Value = "teaching" },
                        new Answer { Content = "Creating practice tests", Value = "practice" }
                    }
                }
            ];
        }
    }
}
