using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Extensions.ForeignEntity
{
    public static partial class IEntityExtensions
    {
        /*
        public static async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(Expression<Func<TSet, bool>> qualifier)
            where TFrom : TranslationContext
            where TTo : TranslationContext
            where TSet : Entity<TSet>
        {
            if (contextFrom.Service == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            var Query = contextFrom.Service.GetRequiredService<IQueryManager>();
            return await Query.Synchronize(contextFrom, contextTo, qualifier);
        }
        */

        public static async Task<List<TSet>> Synchronize<TFrom, TTo, TSet>(this TFrom contextFrom, TTo contextTo, Expression<Func<TSet, bool>> qualifier)
            where TFrom : TranslationContext
            where TTo : TranslationContext
            where TSet : Entity<TSet>
        {
            if (contextFrom.Query == null)
            {
                throw new InvalidOperationException("No service provider.");
            }
            // TODO: contextTO?
            return await contextFrom.Query.Synchronize(contextFrom, contextTo, qualifier);
        }


    }
}
