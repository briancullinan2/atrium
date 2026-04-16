namespace FlashCard.Services;

public interface IStudyService
{
    Task SetStudyMode(bool study);
    bool Study { get; set; }
    event Action<bool>? OnStudyChanged;
}

public class StudyService(IHasClass Classy) : IStudyService
{
    public bool Study { get; set; } = false;

    public event Action<bool>? OnStudyChanged;

    public async Task SetStudyMode(bool study)
    {
        Study = study;
        if (study == true) Classy.ClassNames.Add("study-mode");
        else Classy.ClassNames.Remove("study-mode");
        OnStudyChanged?.Invoke(study);
    }
}
