
namespace FlashCard.Layout;



public interface ICourseMenuItem : INavMenuItem
{
    new IEnumerable<CourseMenuItem> Children { get; set; }
    Type? Lesson { get; set; }
    int? Level { get; set; }
    Type? Course { get; }
    bool IsCourse { get; }
}


public class CourseMenuItem : NavMenuItem<Pages.Course.Course>, ICourseMenuItem
{
    public Type? Lesson { get; set; }
    public int? Level { get; set; }

    public Type? Course => (Lesson != null && CourseMenu.ParentMap.TryGetValue(Lesson, out var parent))
        ? parent
        : Lesson; // If no parent, it IS the course

    public bool IsCourse => Lesson != null && CourseMenu.CourseMap.ContainsKey(Lesson);

    override public string Href
    {
        get
        {
            if (Lesson == null) throw new InvalidOperationException("I hope you find the missing link");

            // Efficient lookup using our cached maps
            var courseType = Course;
            Level = (courseType != null && CourseMenu.CourseMap.TryGetValue(courseType, out var cid)) ? cid : 0;

            // If this item is a top-level course, just return the Level link
            if (Lesson == courseType)
            {
                return TypeExtensions.GetUri<Pages.Course.Course>(c => new() { Level = Level });
            }

            // Otherwise, return the Level + Lesson link
            return TypeExtensions.GetUri<Pages.Course.Course>(c => new()
            {
                Level = Level,
                Lesson = Lesson.Name.ToSafe()
            });
        }
    }

    public new IEnumerable<CourseMenuItem> Children { get => base.Children.OfType<CourseMenuItem>(); set => base.Children = value; }

    //IEnumerable<ICourseMenuItem> INavMenuItem<ICourseMenuItem>.Children { get => Children.OfType<ICourseMenuItem>(); set => Children = value.OfType<CourseMenuItem>(); }
}
