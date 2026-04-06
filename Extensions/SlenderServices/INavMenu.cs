using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices
{
    public interface IHasMenu
    {
        bool Show(NavigationManager nav);
        Task SetMenuActivated(bool menu);
        static abstract string Icon { get; }
    }


    public interface IHasLayout
    {
        bool Show(NavigationManager nav);
        RenderFragment MainLayoutInsert();
    }


    public interface IHasContext
    {
        bool Show(NavigationManager nav);
        RenderFragment ContextInsert();
    }


}