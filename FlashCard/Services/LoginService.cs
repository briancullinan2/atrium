using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace FlashCard.Services
{
    public interface ILoginService
    {
        Task SetLoginMode(bool study);
        Task SetUser(DataLayer.Entities.User? user);
        bool Login { get; set; }
        DataLayer.Entities.User? User { get; set; }
        event Action<bool>? OnLoginChanged;
        event Action<DataLayer.Entities.User?>? OnUserChanged;
    }


    public class LoginService : ILoginService
    {
        public bool Login { get; set; } = false;
        public DataLayer.Entities.User? User { get; set; } = null;

        public event Action<bool>? OnLoginChanged;
        public event Action<DataLayer.Entities.User?>? OnUserChanged;

        public async Task SetLoginMode(bool login)
        {
            Login = login;
            OnLoginChanged?.Invoke(login);
        }

        public async Task SetUser(DataLayer.Entities.User? user)
        {
            User = user;
            OnUserChanged?.Invoke(user);
        }
    }
}
