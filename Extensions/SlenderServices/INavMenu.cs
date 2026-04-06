using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices
{
    public interface IHasMenu : IComponent
    {
        static abstract Delegate ShowMenu { get; }
        Task SetMenuActivated(bool menu);
        static abstract string Icon { get; }
    }


    public interface IHasLayout : IComponent
    {
        static abstract Delegate ShowLayout { get; }
        RenderFragment MainLayoutInsert();
    }


    public interface IHasContext : IComponent
    {
        static abstract Delegate ShowContext { get; }
        RenderFragment ContextInsert();
    }


}