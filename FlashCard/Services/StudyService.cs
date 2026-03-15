namespace FlashCard.Services
{
    public interface IStudyService
    {
        Task SetStudyMode(bool study);
        bool Study { get; set; }
        event Action<bool>? OnStudyChanged;
    }

    public class StudyService : IStudyService
    {
        public bool Study { get; set; } = false;

        public event Action<bool>? OnStudyChanged;

        public async Task SetStudyMode(bool study)
        {
            Study = study;
            OnStudyChanged?.Invoke(study);
        }
    }
}
