using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class Interleaving : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                // Question 1: Text input for "Multiple Sessions"
                new Card
                {
                    Content = "What is it called when you study the same class material for multiple study sessions?",
                    ResponseType = CardType.Short,
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Blocked practice.", Value = "Blocked practice" },
                    },
                },

                // Question 2: Text input for "Another name"
                new Card
                {
                    Content = "What is another name for interleaving?",
                    ResponseType = CardType.Short,
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Varied practice.", Value = "Varied practice" },
                    },
                },

                // Question 3: Radio buttons for True/False
                new Card
                {
                    Content = "When interleaving, alternating similar types of courses is most effective because your brain is already in the right mode.",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "False, try to alternate very different types of subjects if possible. Activating different parts of the brain helps you to remember more information.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "True", Value = "True" },
                        new Answer { Content = "False", Value = "False" }
                    },
                }
            ];
        }
    }
}
