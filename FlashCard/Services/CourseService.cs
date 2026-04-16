
namespace FlashCard.Services;

public interface ICourseService
{
    StepMode? PreviousStep { get; }
    StepMode Step { get; }
    event Action<StepMode?>? OnStepChanged;
    event Action<ICourseMenuItem?>? OnCourseChanged;
    Task SetStep(StepMode? step);
    Task SetCourse(ICourseMenuItem? item);
    ICourseMenuItem? Menu { get; }
    Type? Course { get; }
    Type? Lesson { get; }
    string? Title { get; }
    int? Level { get; }
}

public class CourseService : ICourseService
{
    public event Action<StepMode?>? OnStepChanged;
    public event Action<ICourseMenuItem?>? OnCourseChanged;

    public ICourseMenuItem? Menu { get; private set; } = null;
    public Type? Course { get => Menu?.Course; }
    public Type? Lesson { get => Menu?.Lesson; }
    public int? Level { get => Menu?.Level; }
    public string? Title { get => Menu?.Title; }

    public StepMode? PreviousStep { get; private set; } = null;
    public StepMode Step { get; private set; } = StepMode.Intro;
    public async Task SetStep(StepMode? step)
    {
        PreviousStep = Step;
        Step = step ?? StepMode.Intro;
        OnStepChanged?.Invoke(step);
    }

    public async Task SetCourse(ICourseMenuItem? item)
    {
        PreviousStep = null;
        Menu = item;
        OnCourseChanged?.Invoke(item);
    }
}
