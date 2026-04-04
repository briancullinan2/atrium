namespace Extensions.ExpressYourself
{
    public static class Selectors
    {

        public static int OrderDatabaseQueries(this MethodInfo method)
            => method.IsFilter() ? -2 : method.IsTerminal() ? 2 : 0;

    }
}
