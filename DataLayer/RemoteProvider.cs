using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Linq.Expressions;


namespace DataLayer
{
    public class RemoteProvider : IAsyncQueryProvider
    {
        private readonly IQueryCompiler _compiler;

        public RemoteProvider(IQueryCompiler compiler)
        {
            _compiler = compiler;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            // This creates a new IQueryable tied to THIS provider
            var elementType = expression.Type.GetGenericArguments()[0];
            return (IQueryable)Activator.CreateInstance(
                typeof(EntityQueryable<>).MakeGenericType(elementType),
                new object[] { this, expression }
            )!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments()[0];
            return (IQueryable<TElement>)Activator.CreateInstance(
                typeof(EntityQueryable<>).MakeGenericType(elementType),
                new object[] { this, expression }
            )!;
        }

        public TResult Execute<TResult>(Expression expression)
        {
            // This is where you hand off to your IQueryCompiler
            return _compiler.Execute<TResult>(expression);
        }

        public object? Execute(Expression expression)
        {
            return _compiler.Execute<object>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var cts = new CancellationToken();
            return _compiler.ExecuteAsync<TResult>(expression, cts);
        }

        // You would also implement the Task-based IAsyncQueryProvider methods here...
    }

}
