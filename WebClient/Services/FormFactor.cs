using FlashCard.Services;

namespace WebClient.Services
{
    public class FormFactor : IFormFactor
    {
        public string GetFormFactor()
        {
            return "WebAssembly";
        }

        public string GetPlatform()
        {
            return Environment.OSVersion.ToString();
        }
    }
}
