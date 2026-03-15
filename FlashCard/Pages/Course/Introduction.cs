using DataLayer.Customization;
using DataLayer.Entities;

namespace FlashCard.Pages.Course
{
    public class Introduction : DataLayer.Generators.IGenerator<DataLayer.Entities.Card>
    {
        public static IEnumerable<DataLayer.Entities.Card> Generate()
        {
            return [
                new Card
                {
                    Content = "What grade are you in?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "We are excited to help you learn how to study more effectively.",
                    QuizOnly = true,
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "High school student", Value = "highschool" },
                        new Answer { Content = "College Freshman", Value = "college-freshman" },
                        new Answer { Content = "College Sophomore", Value = "college-sophomore" },
                        new Answer { Content = "College Junior", Value = "college-junior" },
                        new Answer { Content = "College Senior", Value = "college-senior" },
                        new Answer { Content = "Graduate student", Value = "graduate" }
                    }
                },
                new Card
                {
                    Content = "Which do you agree with more regarding academic ability?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "We will talk about this concept in great detail later in the course.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "Some people are born good at academics.", Value = "born", IsCorrect = false },
                        new Answer { Content = "People become good at academics through experience and building skills.", Value = "practice", IsCorrect = true }
                    }
                },
                new Card
                {
                    Content = "How do you manage your time studying for exams?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Our study tools will help you break the procrastination habit.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "I space out my studying far in advance to avoid stress.", Value = "advance", IsCorrect = true },
                        new Answer { Content = "I try to space it out, but usually end up cramming.", Value = "cram", IsCorrect = false },
                        new Answer { Content = "I do my best work under pressure and plan to cram.", Value = "pressure", IsCorrect = false }
                    }
                },
                new Card
                {
                    Content = "How do you manage your electronic devices when you study?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "Get ready to learn how your electronic devices are killing your ability to study effectively.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "I keep them nearby and respond to texts immediately.", Value = "on", IsCorrect = false },
                        new Answer { Content = "I turn them off or put them somewhere they won't distract me.", Value = "off", IsCorrect = true }
                    }
                },
                new Card
                {
                    Content = "How much do you study per day?",
                    ResponseType = CardType.Multiple,
                    ResponseContent = "We will help you develop a plan to make sure that you are spending the right amount of time studying.",
                    Answers = new List<Answer>
                    {
                        new Answer { Content = "0-1 hour", Value = "one" },
                        new Answer { Content = "1-2 hours", Value = "two" },
                        new Answer { Content = "2-4 hours", Value = "four" },
                        new Answer { Content = "4+ hours", Value = "more" }
                    }
                }
            ];
        }
    }
}