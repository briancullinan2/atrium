using StudyModel.Utilities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace StudyModel
{
    public static class QueryFirewall
    {
        public static IEnumerable<Expression> Set(string userId, Type targetTable)
        {
            // looks up what to do based on cross table associations
            //   Where(setting => setting.SetterId == UserId)
            return [];
        }
        public static IQueryable<TEntity> Set<TEntity>(this IQueryable<TEntity> target, string userId)
        {
            var predicates = Set(userId, typeof(TEntity));

            foreach (var expr in predicates)
            {
                // Cast the generic Expression to the specific predicate type
                if (expr is Expression<Func<TEntity, bool>> predicate)
                {
                    target = target.Where(predicate);
                }
            }
            return target;
        }

        public static IQueryable<TEntity> Save<TEntity>(this IQueryable<TEntity> target, string userId)
        {
            // Usually, "Save" firewalls are stricter or check ownership 
            // rather than just visibility.
            var predicates = Save(userId, typeof(TEntity));
            foreach (var expr in predicates)
            {
                if (expr is Expression<Func<TEntity, bool>> predicate)
                {
                    target = target.Where(predicate);
                }
            }
            return target;
        }


        public static IEnumerable<Expression> Save(string userId, Type targetTable)
        {
            // looks up what to do based on the id associations
            //   setting.SetterId == UserId
            return [];
        }

    }
}
