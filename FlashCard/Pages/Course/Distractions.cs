namespace FlashCard.Pages.Course;

public class Distractions : Generators.IGenerator<Card>
{
    public static IEnumerable<Card> Generate()
    {
        return [
            // Question 1: Multitasking Truth
            new Card
            {
                Content = "True or False. You are excellent at multitasking.",
                ResponseType = CardType.Multiple,
                ResponseContent = "You are not good at multitasking despite what you believe.  No one is good at multitasking because that is not how the brain works.",
                Answers =
                [
                    new Answer { Content = "True", Value = "true", IsCorrect = false },
                    new Answer { Content = "False", Value = "false", IsCorrect = true }
                ]
            },

            // Question 2: Downsides of Multitasking
            new Card
            {
                Content = "Which of the following is NOT a downside of multitasking?",
                ResponseType = CardType.Multiple,
                ResponseContent = "The downside of multitasking includes: getting tired more easily, remembering less when you study, and taking longer to study.",
                Answers =
                [
                    new Answer { Content = "Get tired more easily", Value = "tired", IsCorrect = false },
                    new Answer { Content = "Shorter memory of material", Value = "shorter", IsCorrect = true },
                    new Answer { Content = "Remember less", Value = "remember", IsCorrect = false },
                    new Answer { Content = "Takes longer to study", Value = "longer", IsCorrect = false }
                ]
            },

            // Question 3: Impact of Technology Distractions
            new Card
            {
                Content = "How much lower do students interrupted by technology score on tests (in research studies)?",
                ResponseContent = "Research shows that students that have technological distractions score 20% lower on tests than their peers without distractions.  That is like dropping from an A to a C!",
                ResponseType = CardType.Multiple,
                Answers =
                [
                    new Answer { Content = "10%", Value = "10", IsCorrect = false },
                    new Answer { Content = "20%", Value = "20", IsCorrect = true },
                    new Answer { Content = "30%", Value = "30", IsCorrect = false },
                    new Answer { Content = "40%", Value = "40", IsCorrect = false }
                ]
            },

            // Question 4: Recovery Time from Distraction
            new Card
            {
                Content = "How long can a text message distract you from your optimal study state?",
                ResponseType = CardType.Multiple,
                ResponseContent = "Turn off your phone when you study!!!  It may seem like you can get back into the swing of things in 1-3 minutes, but the research shows that it takes 25-40 minutes.  The phone is your greatest enemy while studying and whatever is on it can wait until you are ready to take a study break.",
                Answers =
                [
                    new Answer { Content = "1-3 minutes", Value = "3", IsCorrect = false },
                    new Answer { Content = "5-15 minutes", Value = "15", IsCorrect = false },
                    new Answer { Content = "25-40 minutes", Value = "40", IsCorrect = true },
                    new Answer { Content = "45-60 minutes", Value = "60", IsCorrect = false }
                ]
            }
        ];
    }
}
