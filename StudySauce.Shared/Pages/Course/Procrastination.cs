using DataLayer.Customization;
using DataLayer.Entities;

namespace StudySauce.Shared.Pages.Course
{
    public class Procrastination : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                // Question 1: Types of Memory
                new Card
                {
                    Content = "You have short and long term memory. What are these two types of memory also called?",
                    ResponseType = CardType.Short,
                    ResponseContent = "Your brain has two types of memory, much like a computer has RAM and a hard drive as its short and long term memory. Short term memory is also known as &ldquo;active memory&rdquo; while long term memory is known as &ldquo;reference memory.&rdquo;",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Active Memory", Value = "active memory", IsCorrect = true },
                        new Answer { Content = "Reference Memory", Value = "reference memory", IsCorrect = true }
                    }
                },

                // Question 2: Goal of Studying
                new Card
                {
                    Content = "What is the goal of studying?",
                    ResponseType = CardType.Short,
                    ResponseContent = "The goal of studying is not to do well on your next exam. The goal of studying is to retain the information that you are studying. Acquiring this knowledge is the reason that you are in school. In order to do this, you must commit things to your long term memory",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "To retain information and commit things to long term memory", Value = "retain long term memory", IsCorrect = true }
                    }
                },

                // Question 3: Procrastination Cycle
                new Card
                {
                    Content = "What is the solution to stopping the procrastination to cramming cycle?",
                    ResponseType = CardType.Short,
                    ResponseContent = "Fortunately, there is a simple and extremely effective solution to ending the procrastination to cramming cycle. Space out your studying. This will allow your brain to retain more information and will help you avoid cramming sessions after which you forget everything anyway.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Space out your studying", Value = "space out studying", IsCorrect = true }
                    }
                },

                // Question 4: Tools to reduce Procrastination
                new Card
                {
                    Content = "What are two tools that you can use to help reduce procrastination?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "There are many techniques that will help you to reduce procrastination, but two of the most effective tools are creating and analyzing your deadlines and building a good study plan.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Creating and analyzing deadlines", Value = "deadlines", IsCorrect = true },
                        new Answer { Content = "Building a good study plan", Value = "study plan", IsCorrect = true }
                    }
                }
            ];
        }
    }
}
