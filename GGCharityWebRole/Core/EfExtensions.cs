using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;

namespace GGCharityWebRole
{
    public static class EfExtensions
    {
        public static IQueryable<T> AddIncludes<T>(this IQueryable<T> query, params string[] includes)
        {
            foreach (var include in includes)
            {
                if (!String.IsNullOrWhiteSpace(include))
                {
                    query = query.Include<T>(include);
                }
            }
            return query;
        }
    }
}