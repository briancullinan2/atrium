namespace FlashCard.Pages.Course;

public class GroupStudy : IGenerator<Card>
{
    public static IEnumerable<Card> Generate()
    {
        return [
            // Question 1: Checkbox group for "Bad Times"
            new Card
            {
                Content = "Which of the following are usually bad times to study as a group?",
                ResponseType = CardType.Multiple,
                ResponseContent = "Study groups can be counterproductive when you need to have some peace and quiet (writing papers, memorizing information). They can also be a waste of time if you are not properly prepared (if you are looking at something for the first time).",
                Answers =
                [
                    new Answer { Content = "Writing a paper", Value = "writing" },
                    new Answer { Content = "Trying to clarify difficult concepts", Value = "difficult" },
                    new Answer { Content = "Looking at material for the first time", Value = "material" },
                    new Answer { Content = "Memorizing information", Value = "memorizing" }
                ]
            },

            // Question 2: Radio buttons for Group Size
            new Card
            {
                Content = "How many people should you shoot for when building your study group?",
                ResponseType = CardType.Multiple,
                ResponseContent = "Try to shoot for 3-5 students in order to benefit from a diversity of opinions, but not get bogged down by having too many people competing for air time.",
                Answers =
                [
                    new Answer { Content = "2-3", Value = "2" },
                    new Answer { Content = "3-5", Value = "3" },
                    new Answer { Content = "5-7", Value = "5" },
                    new Answer { Content = "7-10", Value = "7" }
                ]
            },

            // Question 3: Text input for Group Role
            new Card
            {
                Content = "What role should be rotated every week when the study group meets?",
                ResponseType = CardType.Short,
                ResponseContent = "The leader should rotate in order to keep everyone engaged in the group.",
            },

            // Question 4: Radio buttons for True/False
            new Card
            {
                Content = "Study groups should take breaks too.",
                ResponseType = CardType.Multiple,
                ResponseContent = "True. Try not to meet for too long or everyone will get exhausted and you will be less productive. Taking breaks can help keep everyone fresh for longer.",
                Answers =
                [
                    new Answer { Content = "True", Value = "1" },
                    new Answer { Content = "False", Value = "0" }
                ]
            }
        ];
    }
}
