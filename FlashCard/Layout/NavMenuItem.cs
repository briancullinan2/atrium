using DataLayer;
using DataLayer.Utilities.Extensions;
using FlashCard.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;

namespace FlashCard.Layout
{
    public interface INavMenuItem
    {
        string Title { get; set; }
        string Href { get; }
        string Icon { get; set; }
        string? RoleRequired { get; set; }
        bool IsBeta { get; set; }
        bool IsCollapsed { get; set; }
        IEnumerable<INavMenuItem> Children { get; set; }
        DefaultPermissions? Permission { get; set; }
        string? RequiredPermission { get; set; }
    }


    public class NavMenuItem<TComponent> : INavMenuItem
        where TComponent : IComponent, new()
    {
        public NavMenuItem() { }
        public string Title { get; set; } = string.Empty;
        virtual public string Href { get => NavigationExtensions.GetUri(Uri); }
        public Expression<Func<TComponent, TComponent>>? Uri { get; set; } = c => new TComponent();
        public string Icon { get; set; } = "bi-circle";
        public string? RoleRequired { get; set; }
        public bool IsBeta { get; set; } = false;
        public bool IsCollapsed { get; set; } = true; // Added state
        public virtual DefaultPermissions? Permission { get => RequiredPermission?.TryParse<DefaultPermissions>(); set => RequiredPermission = value.ToString(); }
        public virtual string? RequiredPermission { get; set; } = DefaultPermissions.Unset.ToString();
        public virtual IEnumerable<INavMenuItem> Children { get; set; } = [];
    }


    public interface ICourseMenuItem : INavMenuItem
    {
        new IEnumerable<CourseMenuItem> Children { get; set; }
        Type? Lesson { get; set; }
        int? Level { get; set; }
        Type? Course { get; }
        bool IsCourse { get; }
    }


    public class CourseMenuItem : NavMenuItem<Pages.Course.Course>, ICourseMenuItem
    {
        public Type? Lesson { get; set; }
        public int? Level { get; set; }

        public Type? Course => (Lesson != null && CourseMenu.ParentMap.TryGetValue(Lesson, out var parent))
            ? parent
            : Lesson; // If no parent, it IS the course

        public bool IsCourse => Lesson != null && CourseMenu.CourseMap.ContainsKey(Lesson);

        override public string Href
        {
            get
            {
                if (Lesson == null) throw new InvalidOperationException("I hope you find the missing link");

                // Efficient lookup using our cached maps
                var courseType = Course;
                Level = (courseType != null && CourseMenu.CourseMap.TryGetValue(courseType, out var cid)) ? cid : 0;

                // If this item is a top-level course, just return the Level link
                if (Lesson == courseType)
                {
                    return NavigationExtensions.GetUri<Pages.Course.Course>(c => new() { Level = Level });
                }

                // Otherwise, return the Level + Lesson link
                return NavigationExtensions.GetUri<Pages.Course.Course>(c => new()
                {
                    Level = Level,
                    Lesson = Lesson.Name.ToSafe()
                });
            }
        }

        public new IEnumerable<CourseMenuItem> Children { get => base.Children.OfType<CourseMenuItem>(); set => base.Children = value; }

        //IEnumerable<ICourseMenuItem> INavMenuItem<ICourseMenuItem>.Children { get => Children.OfType<ICourseMenuItem>(); set => Children = value.OfType<CourseMenuItem>(); }
    }

}
