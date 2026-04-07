namespace FlashCard.Services;

public interface IStudyService
{
    Task SetStudyMode(bool study);
    bool Study { get; set; }
    event Action<bool>? OnStudyChanged;
}

public class StudyService(IPageManager PageManager) : IStudyService
{
    public bool Study { get; set; } = false;

    public event Action<bool>? OnStudyChanged;

    public async Task SetStudyMode(bool study)
    {
        Study = study;
        if (study == true) PageManager.ClassNames.TryAdd("study-mode", "study-mode");
        else PageManager.ClassNames.TryRemove("study-mode", out _);
        OnStudyChanged?.Invoke(study);
    }
}
