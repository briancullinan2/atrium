using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class TestTaking : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                // Question 1: Radio buttons for Cramming
                new Card
                {
                    Content = "Leading up to the test, it is a super good idea to cram.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "SAY NO TO CRAMMING!!!",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "True", Value = "True" },
                        new Answer { Content = "False", Value = "False" }
                    },
                },

                // Question 2: Text input for Breathing Exercise
                new Card
                {
                    Content = "What is the name of the breathing exercise demonstrated in this video?",
                    ResponseType = CardType.Short,
                    ResponseContent = "It is called four-part breathing. It is also sometimes called combat or tactical breathing."
                },

                // Question 3: Text input for Skimming the Test
                new Card
                {
                    Content = "What should you be looking for when you skim the test?",
                    ResponseType = CardType.Short,
                    ResponseContent = "Skimming the test will help you pace yourself. In particular, look for the number of questions, the type of questions, and the value of questions."
                }
            ];
        }
    }
}
