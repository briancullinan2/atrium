using DataLayer.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataLayer.Generators
{
    public class Permissions : IGenerator<Permission>
    {
        public static IEnumerable<Permission> Generate()
        {
            return [];
        }
    }
}
