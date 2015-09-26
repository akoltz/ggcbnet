using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace GGCharityWebRole
{
    public class GridViewModel
    {
        public GridViewModel(string GridName, string DefaultSort, IEnumerable<dynamic> Elements, IEnumerable<GridColumn> ColumnFormats,
            int PageSize, int TotalElements)
        {
            this.GridName = GridName;
            this.DefaultSort = DefaultSort;
            this.Elements = Elements;
            this.ColumnFormats = ColumnFormats;
            this.PageSize = PageSize;
            this.TotalElements = TotalElements;
        }
        public string GridName;
        public string DefaultSort;
        public IEnumerable<dynamic> Elements;
        public IEnumerable<GridColumn> ColumnFormats;
        public int TotalElements;
        public int PageSize;
    }

    public class GridColumn
    {
        public GridColumn(string Header, Func<dynamic, object> Format, string Style = null)
        {
            this.Header = Header;
            this.Format = Format;
            this.Style = Style;
        }
        public Func<dynamic, object> Format;
        public string Header;
        public string Style;
    }
}