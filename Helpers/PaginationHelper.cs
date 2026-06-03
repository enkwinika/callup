using System;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;

namespace CallUp.Helpers
{
    public class PagedList<T>
    {
        public List<T> Items { get; private set; }
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }

        public PagedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            Items = items;
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public static PagedList<T> Create(IEnumerable<T> source, int pageIndex, int pageSize)
        {
            var count = source.Count();
            var items = source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
            return new PagedList<T>(items, count, pageIndex, pageSize);
        }
    }

    public static class DbConfig
    {
        public static string GetConnectionStringName()
        {
            bool useProd = ConfigurationManager.AppSettings["UseProductionDb"] == "true";
            return useProd ? "CallUpContextProd" : "CallUpContext";
        }

        public static string GetConnectionString()
        {
            string name = GetConnectionStringName();
            return ConfigurationManager.ConnectionStrings[name].ConnectionString;
        }
    }
}
