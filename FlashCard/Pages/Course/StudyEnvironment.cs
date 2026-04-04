namespace FlashCard.Pages.Course
{
    public class StudyEnvironment : Generators.IGenerator<Card>
    {
        public static IEnumerable<Card> Generate()
        {
            return [
                // Question 1: Studying in Bed
                new Card
                {
                    Content = "Your bed is a great place to study since getting comfortable is critical to memory retention.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "False - Your brain associates your bed with sleeping, so studying on your bed can lead to increased drowsiness.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "1", IsCorrect = false },
                        new Answer { Content = "False - Your brain associates your bed with sleeping, so studying on your bed can lead to increased drowsiness.", Value = "0", IsCorrect = true }
                    ]
                },

                // Question 2: Mozart Effect
                new Card
                {
                    Content = "Listening to Mozart is proven to help you study better.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "False - The research does not conclusively prove this.  However, Mozart and other soothing instrumental music is better than listening to music with lyrics.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "1", IsCorrect = false },
                        new Answer { Content = "False - The research does not conclusively prove this. However, Mozart and other soothing instrumental music is better than listening to music with lyrics.", Value = "0", IsCorrect = true }
                    ]
                },

                // Question 3: Nature Walks
                new Card
                {
                    Content = "A nature walk can be an effective way to take a break between study sessions.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "True - Research shows that taking a walk in natural surroundings can actually improve your ability to remember what you are studying.",
                    Answers =
                    [
                        new Answer { Content = "True - Research shows that taking a walk in natural surroundings can actually improve your ability to remember what you are studying.", Value = "1", IsCorrect = true },
                        new Answer { Content = "False", Value = "0", IsCorrect = false }
                    ]
                },

                // Question 4: Study Session Length
                new Card
                {
                    Content = "Your study sessions should last a minimum of 1 hour and ideally you should stick with a topic for several hours to get the greatest benefit of prolonged focus.",
                    ResponseType = CardType.TrueFalse,
                    ResponseContent = "False - Taking breaks is a critical component of studying.  Try to study for 50-60 minutes before taking a 10 minute break.  Alternatively, try to study for 25-30 minutes with a 5 minute break if you find the shorter sessions are more effective for you.",
                    Answers =
                    [
                        new Answer { Content = "True", Value = "1", IsCorrect = false },
                        new Answer { Content = "False - Taking breaks is a critical component of studying. Try to study for 50-60 minutes before taking a 10 minute break.", Value = "0", IsCorrect = true }
                    ]
                }
            ];
        }
    }
}
