using System.Reflection;

namespace RazorSharp.Services
{
    public interface ITitleService
    {
        Task<string?> UpdateTitle(string? title);
        event Action<string?>? OnTitleChanged;
    }



    public class TitleService : ITitleService
    {
        public static readonly string? AppName = Assembly.GetEntryAssembly()?
                             .GetCustomAttribute<AssemblyProductAttribute>()?
                             .Product;

        public event Action<string?>? OnTitleChanged;
        public virtual async Task<string?> UpdateTitle(string? title)
        {
            if (title == null)
            {
                title = AppName;
            }
            else
            {
                title = title + " - " + AppName;
            }
            OnTitleChanged?.Invoke(title);
            return title;
        }
    }

}
