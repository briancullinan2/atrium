using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DataLayer
{
    public static class Selectors
    {

        public static Setting? Where(this IEnumerable<Setting> query, DefaultPermissions setting)
            => query.FirstOrDefault(s => s.Name == setting.ToString());


        public static int OrderDatabaseQueries(this MethodInfo method)
            => method.IsFilter() ? -2 : method.IsTerminal() ? 2 : 0;
        

    }
}
