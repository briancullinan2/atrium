using System.Reflection;

namespace UserData;



[Obfuscation(Exclude = true, ApplyToMembers = true)]
public enum Gender : int
{
    Female = 1,
    Male = 2,
    Other = 3,
    Unspecified = 0
}



