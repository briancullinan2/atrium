using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.SlenderServices;


#if false
public class DelegateAsyncProxy<T>(IPageManager parent, PageAction action)

{
    // Usage: Page.OnScroll["Navlist"] += ...
    public virtual Func<string, T?, Task>? this[string id]
    {
        get
        {
            return parent[action, id] as Func<string, T?, Task>;
        }
        set
        {
            parent.Subscribe((action, id), value);
        }
    }
}

public class DelegateProxy<T>(IPageManager parent, PageAction action)

{
    // Usage: Page.OnScroll["Navlist"] += ...
    public virtual Action<string, T?>? this[string id]
    {
        get
        {
            return parent[action, id] as Action<string, T?>;
        }
        set
        {
            parent.Subscribe((action, id), value);
        }
    }
}

public class StringProxy(IPageManager parent, PageAction action)
    : DelegateProxy<string>(parent, action)
{

}

public class BooleanProxy(IPageManager parent, PageAction action)
    : DelegateProxy<bool>(parent, action)
{

}

public class ResizeProxy(IPageManager parent)
    : DelegateProxy<(int w, int h, bool s)>(parent, PageAction.Resize)
{
}


public class StringAsyncProxy(IPageManager parent, PageAction action)
    : DelegateAsyncProxy<string>(parent, action)
{

}

public class BooleanAsyncProxy(IPageManager parent, PageAction action)
    : DelegateAsyncProxy<bool>(parent, action)
{

}

public class ResizeAsyncProxy(IPageManager parent)
    : DelegateAsyncProxy<(int w, int h, bool s)>(parent, PageAction.Resize)
{
}

#endif
