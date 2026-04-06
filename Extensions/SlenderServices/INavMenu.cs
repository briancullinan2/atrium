using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices
{
    public interface INavMenu
    {
        bool Show { get; }
        Task SetMenuActivated(bool menu);
        string Icon { get; }
    }
}
