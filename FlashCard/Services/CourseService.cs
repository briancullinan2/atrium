
namespace FlashCard.Services;

public interface ICourseService
{
    StepMode? Step { get; set; }
    event Action<StepMode?>? OnStepChanged;
    event Action<ICourseMenuItem?>? OnCourseChanged;
    Task SetStep(StepMode? step);
    Task SetCourse(ICourseMenuItem? item);
}

public class CourseService : ICourseService
{
    public event Action<StepMode?>? OnStepChanged;
    public event Action<ICourseMenuItem?>? OnCourseChanged;
    public ICourseMenuItem? Course { get; set; } = null;

    public StepMode? Step { get; set; } = StepMode.Intro;
    public async Task SetStep(StepMode? step)
    {
        Step = step;
        OnStepChanged?.Invoke(step);
    }

    public async Task SetCourse(ICourseMenuItem? item)
    {
        Course = item;
        OnCourseChanged?.Invoke(item);
    }
}
