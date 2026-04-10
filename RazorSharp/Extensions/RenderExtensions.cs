using Microsoft.AspNetCore.Components.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace RazorSharp.Extensions
{
    public static class RenderExtensions
    {

        public static void NavigateTo<TComponent>(this NavigationManager Nav, Expression<Func<TComponent, TComponent>>? initializer = null) where TComponent : class
        {
            if (initializer == null)
            {
                Nav.NavigateTo(TypeExtensions.GetUri<TComponent>());
                return;
            }
            Nav.NavigateTo(TypeExtensions.GetUri(initializer));
        }


        public static RenderFragment Coordinate(params RenderFragment[] fragments)
        {
            return (__builder) =>
            {
                var seq = 1;
                foreach (var fragment in fragments)
                {
                    __builder.OpenRegion(seq * 100); // Start a new "coordinate space"
                    fragment(__builder); // This method can safely start at 0
                    __builder.CloseRegion();
                }
            };
        }



        public static RenderFragment ToNavLink<TComponent>(
            Expression<Func<TComponent, TComponent>>? initializer = null
            , string? overrideTitle = null
            , string? overrideIcon = null)
            where TComponent : class, IComponent
        {
            var values = initializer?.ToDictionary();
            var uri = TypeExtensions.GetUri<TComponent>(values);
            return ToNavLink<TComponent>(uri, overrideTitle, overrideIcon);
        }

        public static RenderFragment ToNavLink<TComponent>(
            string uri
            , string? overrideTitle = null
            , string? overrideIcon = null)
            where TComponent : class, IComponent
        {
            var type = typeof(TComponent);
            return ToNavLink(uri, overrideTitle, overrideIcon, type);
        }

        public static RenderFragment ToNavLink(
            string uri
            , string? overrideTitle = null
            , string? overrideIcon = null
            , Type? type = null)
        {
            var display = type?.GetCustomAttributes<DisplayAttribute>().FirstOrDefault();
            return ToNavLink(uri, overrideTitle ?? display?.ShortName ?? display?.Name, overrideIcon ?? display?.Prompt);
        }

        public static RenderFragment ToNavLink(
            string uri
            , string? overrideTitle = null
            , string? overrideIcon = null)
        {
            return (__builder) =>
            {
                __builder.OpenComponent<NavLink>(0);
                __builder.AddAttribute(1, "class", "header-link");
                __builder.AddAttribute(2, "href", uri);
                if(string.IsNullOrWhiteSpace(uri))
                    __builder.AddAttribute(3, "Match", NavLinkMatch.All);
                else
                    __builder.AddAttribute(3, "Match", NavLinkMatch.Prefix);

                // The inner HTML of a component is passed via the "ChildContent" attribute
                __builder.AddAttribute(4, "ChildContent", (RenderFragment)((__builder2) =>
                {
                    // 1. The <i> icon tag
                    __builder2.OpenElement(0, "i");
                    __builder2.AddAttribute(1, "class", $"bi ${overrideIcon}");
                    __builder2.AddAttribute(2, "aria-hidden", "true");
                    __builder2.CloseElement();

                    // 2. The <span> text tag
                    __builder2.OpenElement(3, "span");
                    __builder2.AddAttribute(4, "class", "header-text");
                    __builder2.AddContent(5, overrideTitle);
                    __builder2.CloseElement();
                }));

                __builder.CloseComponent();
            };
        }


    }
}
