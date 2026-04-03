using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.ForeignEntity
{
    public static partial class IEntityExtensions
    {


        public static async Task<TEntity?> EntryAsync<TEntity>(this DbContext context, TEntity entity)
            where TEntity : Entity<TEntity>
        {
            var lambda = Predicate(entity);
            return await context.Set<TEntity>().FirstOrDefaultAsync(lambda);
        }



        public static async Task<bool> ExistsAsync<TEntity>(this DbContext context, TEntity entity)
            where TEntity : Entity<TEntity>
        {
            return (await EntryAsync(context, entity)) != null;
        }

    }
}
