using System;
using System.Collections.Generic;
using System.Text;

namespace DataStore
{


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum ControlMode : int
    {
        Unset = 0,
        View = 1,
        Edit = 2,
        Owner = 3,
        Group = 4,
        Result = 5,
        List = 6,
        Add = 7,
        Delete = 8,
    }

}
