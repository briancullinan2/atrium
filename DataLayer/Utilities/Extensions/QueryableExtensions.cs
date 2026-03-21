using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using static Android.Renderscripts.ScriptGroup;


namespace DataLayer.Utilities.Extensions
{
    public static partial class QueryableExtensions
    {
        // TODO: QueryManager.Query(string).Any(u => !u.IsDeleted) 
        // TODO: QueryManager.Query<User>().Where(string) 
        // TODO: QueryManager.Query<User>().OrderBy(string) 
        // 

        public static IQueryable<TResult> ToMember<TEntity, TResult>(this IQueryable<TEntity> query, string queryString)
        {
            //return queryString.ToMember()
        }

        public enum CombineMode
        {
            OrElse,  // Default: Any match
            AndAlso  // Strict: All must match
        }

        /// <summary>
        /// Folds multiple expressions into a single tree based on the specified mode.
        /// </summary>
        /// <param name="expressions">The list of property/value expressions to combine.</param>
        /// <param name="mode">Whether to use OR or AND logic between elements.</param>
        /// <param name="isNegated">If true, the entire resulting group is wrapped in Expression.Not.</param>
        public static Expression? Combine(CombineMode mode = CombineMode.OrElse,
            bool isNegated = false,
            params IEnumerable<Expression> expressions
        ) {
            if (expressions == null || !expressions.Any())
                return null;

            Expression? root = null;

            foreach (var expr in expressions)
            {
                if (expr == null) continue;

                if (root == null)
                {
                    root = expr;
                }
                else
                {
                    root = mode == CombineMode.AndAlso
                        ? Expression.AndAlso(root, expr)
                        : Expression.OrElse(root, expr);
                }
            }

            return isNegated ? Expression.Not(root!) : root;
        }


        [GeneratedRegex(@"[~!](?<exclusion>[^\s]+)|(?<column>(?<columnName>[^\s]+):([~!](?<columnExclusion>[^\s]+)|(?<columnTerm>[^\s]+)))|(?<term>[^\s]+)", RegexOptions.IgnorePatternWhitespace)]
        private static partial Regex KeyValueSearchParser();


        public static string? Success(this GroupCollection collection, string name)
            => collection[name].Success ? collection[name].Value : null;


        public static (string Name, string Term, bool Exclusion) ParseSearch(string search)
            => KeyValueSearchParser().Match(search) is { } match 
            ? (
                Name: match.Groups.Success("columnName") ?? string.Empty, 
                Term: match.Groups.Success("exclusion") ?? match.Groups.Success("columnExclusion") 
                    ?? match.Groups.Success("columnTerm") ?? match.Groups.Success("term") ?? string.Empty,
                match.Groups.Success("exclusion") != null || match.Groups.Success("columnExclusion") != null) 
            : (
                Name: string.Empty, 
                Term: string.Empty, 
                false
            );


        public static Expression? ToSearch(this Expression left, string? search)
            => (Nullable.GetUnderlyingType(left.Type) ?? left.Type) switch
            {

            };


        /// <summary>
        /// Creates a single Equal comparison. 
        /// If the property is Boolean, it attempts to parse the term.
        /// </summary>
        public static BinaryExpression? CreateEquality(Expression property, string term)
        {
            // 1. Handle Boolean conversion specifically
            if (property.Type == typeof(bool) || property.Type == typeof(bool?))
            {
                true.TryParse()
                if (bool.TryParse(term, out bool boolValue))
                {
                    return Expression.Equal(property, Expression.Constant(boolValue));
                }

                // If it's not a direct "true/false", we skip or return null 
                // to prevent invalid comparisons like IsDeleted == "brian"
                return null;
            }

            // 2. Default fallback for other primitive types (assuming term matches type)
            // For strings or others, you'd just use Expression.Constant(term)
            var convertedTerm = Convert.ChangeType(term, property.Type);
            return Expression.Equal(property, Expression.Constant(convertedTerm));
        }


        public static Expression? BuildComparator(MemberExpression member, string term, bool useIndexOf = false)
        {
            var type = member.Type;
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            // Switch on the underlying type for the "fancy" mapping
            return underlyingType switch
            {
                var t when t == typeof(string) => BuildStringCheck(member, term, useIndexOf),
                var t when t == typeof(bool) => BuildBooleanCheck(member, term),
                var t when t.IsEnum => BuildEnumCheck(member, term),
                var t when t == typeof(DateTime) => BuildDateTimeCheck(member, term, useIndexOf),
                _ => BuildDefaultCheck(member, term, useIndexOf) // Handle ints, decimals, etc.
            };
        }

        public static IOrderedQueryable<TEntity> Sort<TEntity>(
            this IQueryable<TEntity> source,
            ColumnCollection<TEntity> columns,
            IEnumerable<DataTablesRequestModel.Sort> sorts)
        {
            IOrderedQueryable<TEntity>? resultOrdered = null;
            var parameter = Expression.Parameter(typeof(TEntity), "m");

            // Pre-calculate member expressions to avoid repeated reflection in the loop
            var validColumns = GetValidMemberExpressions(columns, parameter);

            foreach (var sort in sorts)
            {
                if (sort.iSortCol < 0 || sort.iSortCol >= columns.Count) continue;

                var targetColumn = columns[sort.iSortCol];
                if (!validColumns.TryGetValue(targetColumn, out var body)) continue;

                bool isAscending = sort.sSortDir?.ToLower() == "asc";
                bool isFirstSort = resultOrdered == null;

                // Determine the correct method name based on sequence and direction
                string methodName = isFirstSort
                    ? (isAscending ? nameof(Queryable.OrderBy) : nameof(Queryable.OrderByDescending))
                    : (isAscending ? nameof(Queryable.ThenBy) : nameof(Queryable.ThenByDescending));

                var lambda = Expression.Lambda(body, parameter);

                // Build the method call
                var methodCall = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    [ typeof(TEntity), body.Type ],
                    isFirstSort ? source.Expression : resultOrdered!.Expression,
                    Expression.Quote(lambda)
                );

                resultOrdered = (IOrderedQueryable<TEntity>)source.Provider.CreateQuery<TEntity>(methodCall);
            }

            // Fallback: If no valid sorts, return the original source (or throw if you prefer)
            return resultOrdered ?? throw new ArgumentException("No valid sort columns provided.", nameof(sorts));
        }

        /// <summary>
        /// Generates an expression to pass to IQueryable functions, just like LINQ syntax but done with code.
        /// TODO: Optimization marks the only filter done on properties based on types
        /// TODO: Split term marks where search terms are split in to seperate matches rather than the whole string
        /// TODO: NOT marks there search terms are marked with a ! and the inverse is used
        /// TODO: Search terms marks where terms can be input in two formats, using ColumnName:term, or the search enumerable parameter from the datatables model
        /// </summary>
        /// <param name="source"></param>
        /// <param name="columns"></param>
        /// <param name="search"></param>
        /// <param name="useIndexOf"> </param>
        /// <returns></returns>
        public static IQueryable<TEntity> Search<TEntity>(this IQueryable<TEntity> source, IEnumerable<DataTableColumn<TEntity>> columns, string search, bool useIndexOf = false)
        {
            // this parameter is created for passing the TEntity in to when the query is evaluated
            var parameter = Expression.Parameter(typeof(TEntity), "m");

            // this is the method used below to determine if a property contains the search terms
            var containsMethod = typeof(string).GetMethod("Contains");

            // get the columns set to valid properties
            //Dictionary<DataTableColumn<TEntity>, KeyValuePair<MemberExpression, ParameterExpression>> collections;
            /* Also supports
             * x => x.UserAssignedLocations.Any(y => y.Name.IndexOf("search") != -1)
             */
            var validColumns = GetValidMemberExpressions(columns, parameter).Where(x => x.Key.CanSearch);
            Dictionary<DataTableColumn<TEntity>, List<string>> columnTerms;
            Dictionary<DataTableColumn<TEntity>, List<string>> columnExclusions;
            GetSearchTerms(validColumns.Select(x => x.Key), search, out columnTerms, out columnExclusions);

            // create an expression to pass to the IQueryable.Where<>() function
            Expression termOr = null;
            Expression excAnd = null;
            foreach (var validColumn in validColumns)
            {
                if (!columnTerms.ContainsKey(validColumn.Key))
                    continue;
                var terms = columnTerms[validColumn.Key].Distinct().Where(x => x.Trim() != "");
                var property = validColumn.Value;

                // if validColumn is a collection of items create the search on the inner expression and handle it below
                if (collections != null && collections.ContainsKey(validColumn.Key))
                {
                    property = collections[validColumn.Key].Key;
                }

                // TODO: handles Nullable<> types and converts them to their object types
                MemberExpression nullableProperty = null;
                if (property.Type.IsGenericType && property.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    nullableProperty = property;
                    property = Expression.MakeMemberAccess(property, property.Type.GetProperty("Value"));
                }

                TypeCode typeCode = Type.GetTypeCode(property.Type);

                // TODO: Optimization
                // determine if search string is numeric, used to match types below
                decimal outVar;
                var isNumeric = terms.Any(x => decimal.TryParse(x, out outVar));

                // if search string is not numeric but the column is, they will never match
                if (!isNumeric && !(
                    // this list is object, strings, and all the other special cases handled below
                    //  this is supposed to be everything except numeric types
                    typeCode == TypeCode.Object || typeCode == TypeCode.Char || typeCode == TypeCode.String ||
                                    typeCode == TypeCode.DateTime || typeCode == TypeCode.Boolean || property.Type.IsEnum))
                    continue;

                foreach (var term in terms)
                {
                    Expression right = null;

                    // if the property if not of type string, convert it, all LLBLGen properties should be a primitive type
                    if (property.Type != typeof(string))
                    {
                        if (property.Type == typeof(Boolean))
                        {
                            // TODO: translate true and false
                            if (true.ToString().ToLower().Contains(term.ToLower()))
                                right = Expression.Equal(property, Expression.Constant(true)); //(Expression<Func<bool, bool>>)(p => p);
                            if (false.ToString().ToLower().Contains(term.ToLower()))
                                right = right != null
                                            ? Expression.OrElse(right,
                                                                Expression.Equal(property, Expression.Constant(false)))
                                            : Expression.Equal(property, Expression.Constant(false));
                        }
                        else if (property.Type.IsEnum)
                        {
                            // TODO: translate enums
                            // convert the Enum to string and search the string for the text
                            var matches =
                                Enum.GetNames(property.Type)
                                    .Select(x =>
                                    {
                                        return new KeyValuePair<string, string>(x,
                                                                     (Enum.Parse(property.Type, x) as Enum).
                                                                         GetDisplayText());
                                    })
                                    .Where(x => x.Value.IndexOf(term, StringComparison.InvariantCultureIgnoreCase) > -1)
                                    .Select(x => Enum.Parse(property.Type, x.Key, true));

                            // loop through each matches enum value and search for that
                            var enumType = Enum.GetUnderlyingType(property.Type);
                            foreach (var match in matches)
                            {
                                right = right != null
                                            ? Expression.OrElse(right,
                                                                Expression.Equal(Expression.Convert(property, enumType),
                                                                                 Expression.Constant(
                                                                                     Convert.ChangeType(match,
                                                                                                        enumType))))
                                            : Expression.Equal(Expression.Convert(property, enumType),
                                                               Expression.Constant(Convert.ChangeType(match, enumType)));
                            }
                        }
                        else if (property.Type == typeof(DateTime))
                        {
                            MethodCallExpression access = null;
                            var method = typeof(DateTime).GetMethod("ToString", []);
                            if (validColumn.Key.Format != null && validColumn.Key.Format.Body.ToString().Contains("ConvertTime"))
                            {
                                // TODO: Localize to user's time
                                var addhours = typeof(DateTime).GetMethod("AddHours", [typeof(double)]);
                                var timezone = ((TimeZoneInfo)HttpContext.Current.Items["TimeZoneInfo"]);
                                access = Expression.Call(
                                    Expression.Call(
                                        property,
                                        addhours,
                                        Expression.Constant((double)timezone.BaseUtcOffset.Hours)),
                                    method,
                                    null);
                                // convert the object to a string and test for the search string
                                /*
                                // use the daylight savings time rule to modify the time server side and look for matches
                                // adjust for timezone
                                // adjust for daylight savings time
                                foreach(var rule in timezone.GetAdjustmentRules())
                                {
                                    // if date >= DaylightTransitionStart && date <= DaylightTransitionEnd
                                    var startEnd = Expression.AndAlso(
                                        Expression.GreaterThanOrEqual(property,
                                                                      Expression.Constant(rule.DaylightTransitionStart)),
                                        Expression.LessThanOrEqual(property, Expression.Constant(rule.DaylightTransitionEnd)));
                                    // if condition then use BaseUtcOffset + DaylightDelta
                                    conditional = Expression.IfThenElse(startEnd, 
                                        Expression.Call(property, addhours, Expression.Constant((double)timezone.BaseUtcOffset.Hours +
                                        rule.DaylightDelta.Hours)), conditional);
                                }
                                access = Expression.Call(access, method, null);
                            */
                            }
                            else
                                access = Expression.Call(property, method, null);
                            // search for term
                            if (useIndexOf)
                            {
                                var indexOf = Expression.Call(access, "IndexOf", null,
                                                                Expression.Constant(term, typeof(string)),
                                                                Expression.Constant(StringComparison.OrdinalIgnoreCase));
                                right = Expression.NotEqual(indexOf, Expression.Constant(-1));
                            }
                            else
                            {
                                right = Expression.Call(access, containsMethod,
                                                        Expression.Constant(term, typeof(string)));
                            }
                        }
                        else
                        {
                            // convert the object to a string and test for the search string
                            var method = typeof(Convert).GetMethod("ToString", [property.Type]);
                            var access = Expression.Call(null, method, property);
                            if (method != null)
                            {
                                if (useIndexOf)
                                {
                                    var indexOf = Expression.Call(access, "IndexOf", null,
                                                                  Expression.Constant(term, typeof(string)),
                                                                  Expression.Constant(StringComparison.OrdinalIgnoreCase));
                                    right = Expression.NotEqual(indexOf, Expression.Constant(-1));
                                }
                                else
                                {
                                    right = Expression.Call(access, containsMethod,
                                                            Expression.Constant(term, typeof(string)));
                                }
                            }
                        }
                    }
                    else
                    {
                        // already a string, just perform IndexOf()
                        if (useIndexOf)
                        {
                            var indexOf = Expression.Call(property, "IndexOf", null,
                                                          Expression.Constant(term, typeof(string)),
                                                          Expression.Constant(StringComparison.OrdinalIgnoreCase));
                            right = Expression.NotEqual(indexOf, Expression.Constant(-1));
                        }
                        else
                        {
                            right = Expression.Call(property, containsMethod, Expression.Constant(term, typeof(string)));

                        }
                    }

                    // TODO: handles Nullable<> types and converts them to their object types
                    if (nullableProperty != null && right != null)
                    {
                        // x => x.HasValue && right
                        right =
                            Expression.AndAlso(
                                Expression.Equal(
                                    Expression.MakeMemberAccess(nullableProperty,
                                                                nullableProperty.Type.GetProperty("HasValue")),
                                    Expression.Constant(true)),
                                right);
                    }

                    // TODO Collections matches Any() in collections
                    if (collections != null && right != null && collections.ContainsKey(validColumn.Key))
                    {
                        var objectType =
                            validColumn.Value.Type.GenericImplementsType(typeof(IEnumerable<>)).GetGenericArguments()[0];

                        var funcType = typeof(Func<,>).MakeGenericType(objectType, typeof(bool));
                        var any =
                            typeof(Enumerable).GetMethods().First(x => x.Name == "Any" && x.GetParameters().Count() == 2);
                        /*
                         * Func<objectType,bool> = x => collections[validColumn.Key].IndexOf(search) != -1
                         */
                        var newParam = collections[validColumn.Key].Value;
                        var lambda = (Expression)Expression.Lambda(funcType, right, newParam);

                        /*
                         * validColumn.Value.Any(Func<objectType,bool)
                         */
                        right = Expression.Call(
                            null,
                            any.MakeGenericMethod(objectType),
                            [validColumn.Value, lambda]);
                    }

                    // TODO: NOT operations to the oposite and are ANDed together rather than ORed
                    // null can occur if the property is an ENUM and there are not matches
                    if (right != null)
                    {
                        if (columnExclusions.ContainsKey(validColumn.Key) &&
                            columnExclusions[validColumn.Key].Contains(term))
                            // inverse if excluding the match with ~
                            excAnd = excAnd != null
                                         ? Expression.AndAlso(excAnd, Expression.Not(right))
                                         : (Expression)Expression.Not(right);
                        else
                            // put OrElse expressions between each field
                            termOr = termOr != null
                                         ? Expression.OrElse(termOr, right)
                                         : right;
                    }

                    // end term loop
                }

                // end property loop
            }

            // create the body which is a combination of the inclusion terms AND excluding the terms marked with ~
            Expression? body = null;
            if (termOr != null)
                body = termOr;
            if (excAnd != null)
                body = termOr != null ? Expression.AndAlso(termOr, excAnd) : excAnd;
            if (body != null)
            {
                // pass the lambda expression to IQueryable.Where<>()
                var lambda = Expression.Lambda(body, [ parameter ]);

                return source.Provider.CreateQuery<TEntity>(
                    Expression.Call(
                        null,
                        typeof(Queryable).GetMethods().First(x => x.Name == "Where").MakeGenericMethod(typeof(TEntity)),
                        [ source.Expression, Expression.Quote(lambda) ]
                    ));
            }

            // no valid columns where found for searching
            return source;
        }



        public static void GetSearchTerms<TEntity>(
            IEnumerable<DataTableColumn<TEntity>> validColumns,
            string? search,
            out Dictionary<DataTableColumn<TEntity>, List<string>> outTerms,
            out Dictionary<DataTableColumn<TEntity>, List<string>> outExclusions)
        {
            var columnTerms = validColumns.ToDictionary(x => x, _ => new List<string>());
            var columnExclusions = validColumns.ToDictionary(x => x, _ => new List<string>());

            if (string.IsNullOrWhiteSpace(search))
            {
                outTerms = columnTerms;
                outExclusions = columnExclusions;
                return;
            }

            search = search.Trim();

            // 1. Handle Literal Full String Search
            if (search.StartsWith('\"') && search.EndsWith('\"') && search.Length >= 2)
            {
                var literal = search[1..^1];
                foreach (var col in columnTerms.Values) col.Add(literal);
            }
            else
            {
                // 2. Complex Regex Parsing
                var matches = KeyValueSearchParser().Matches(search);

                foreach (Match match in matches)
                {
                    if (match.Groups["exclusion"].Success)
                    {
                        var val = match.Groups["exclusion"].Value;
                        foreach (var list in columnExclusions.Values) list.Add(val);
                    }
                    else if (match.Groups["column"].Success)
                    {
                        var colName = match.Groups["columnName"].Value;
                        var column = validColumns.FirstOrDefault(x =>
                            string.Equals(x.ColumnName, colName, StringComparison.OrdinalIgnoreCase) ||
                            x.ColumnName.EndsWith("." + colName, StringComparison.OrdinalIgnoreCase));

                        if (column != null)
                        {
                            if (match.Groups["columnExclusion"].Success)
                                columnExclusions[column].Add(match.Groups["columnExclusion"].Value);
                            else
                                columnTerms[column].Add(match.Groups["columnTerm"].Value);
                        }
                        else
                        {
                            // Fallback: treat the whole "col:term" as a general term
                            var fullMatch = match.Groups["column"].Value;
                            foreach (var list in columnTerms.Values) list.Add(fullMatch);
                        }
                    }
                    else if (match.Groups["term"].Success)
                    {
                        var val = match.Groups["term"].Value;
                        if (!string.IsNullOrEmpty(val))
                            foreach (var list in columnTerms.Values) list.Add(val);
                    }
                }
            }

            outTerms = columnTerms;
            outExclusions = columnExclusions;
        }


        /// <summary>
        /// Replaces GetMemberExpressions by mapping your ColumnCollection to 
        /// valid MemberExpressions using the new logic.
        /// </summary>
        public static Dictionary<DataTableColumn<TEntity>, MemberExpression> GetValidMemberExpressions<TEntity>(
            this IEnumerable<DataTableColumn<TEntity>> columns,
            ParameterExpression parameter)
        {
            var result = new Dictionary<DataTableColumn<TEntity>, MemberExpression>();

            foreach (var col in columns)
            {
                var access = col.ColumnName.ToMember();
                if (access != null)
                {
                    result.Add(col, access);
                }
            }

            return result;
        }

    }
}
