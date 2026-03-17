using DataLayer;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class ActiveReading : DataLayer.Generators.IGenerator<Card>
    {
        public static IEnumerable<Card> Generate()
        {
            return [
                // Question 1: Textarea for Active Reading definition
                new Card
                {
                    Content = "What is active reading?",
                    ResponseType = CardType.Short, // Mapping from <textarea>
                    ResponseContent = "Active reading is simply trying to understand what you are reading and recognizing which parts are most important to your needs. The key to active reading is being curious about what you are covering.",
                },

                // Question 2: Radio buttons for Highlighting
                new Card
                {
                    Content = "Highlighting and underlining is an effective tool for active reading.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "False. People have been lying to you for years... Highlighting and underlining is a complete waste of time and won't help you remember anything. In order to learn the material, you have to go one step further. Convert whatever you are highlighting and underlining into an exercise that will help you commit the material to memory.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "True" },
                        new Answer { Content = "False", Value = "False" }
                    ],
                },

                // Question 3: Radio buttons for Skimming
                new Card
                {
                    Content = "Skimming through the reading is an effective tool for active reading.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "True. Use this technique to get curious about the topic before you start reading. Pay particular attention to learning objectives, chapter summaries, charts, tables, and section headings. The author is trying to tell you what is important and what isn't. Take the hint.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "True" },
                        new Answer { Content = "False", Value = "False" }
                    ],
                },

                // Question 4: Radio buttons for Self-explanation
                new Card
                {
                    Content = "Self-explanation is an effective tool for active reading",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "True. Pause periodically when reading and try to explain what is going on in the text. This will help you stop spacing out in the middle of your reading sessions and will also help you maintain your curiosity.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "True" },
                        new Answer { Content = "False", Value = "False" }
                    ],
                }
            ];
        }
    }
}
