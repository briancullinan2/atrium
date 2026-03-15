using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class TeachToLearn : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                // Question 1: Text input for New Language comparison
                new Card
                {
                    Content = "Why is using the teaching to learn strategy similar to learning a new language?",
                    ResponseType = CardType.Short,
                    ResponseContent = "They are similar because you lose the ability to guess the answer based on context and because you are forced to understand the information at a deeper level when you have to explain it.",
                },

                // Question 2: Radio buttons for Memorizing strategy
                new Card
                {
                    Content = "True or false, teaching to learn is an effective strategy for memorizing lots of information.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "False. Teaching to learn should be used when you need to understand a topic more deeply. For memorizing, there are better strategies to use.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "True", Value = "True" },
                        new Answer { Content = "False", Value = "False" }
                    }
                },

                // Question 3: Text input for Videotaping
                new Card
                {
                    Content = "Why is videotaping yourself explaining a concept particularly helpful?",
                    ResponseType = CardType.Short,
                    ResponseContent = "First, when you have a video recorded, you can tell immediately if you don't understand the material. Second, everyone wants to look good on camera. This will actually help you think through the response more deeply and will help force yourself to learn the concept.",
                }
            ];
        }
    }
}
