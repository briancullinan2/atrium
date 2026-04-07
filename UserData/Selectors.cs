

namespace UserData;

public static class Selectors
{

    public static Setting? Where(this IEnumerable<Setting> query, DefaultPermissions setting)
        => query.FirstOrDefault(s => s.Name == setting.ToString());



}
