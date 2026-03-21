using DataLayer.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;

namespace DataLayer.Utilities
{
    public interface IDataTableColumn
    {
        bool CanSort { get; set; }
        string ColumnName { get; set; }
        string Header { get; set; }
        string Style { get; set; }
        bool Visible { get; set; }
        string Toggle { get; set; }
    }

    [DataContract]
    public class DataTableColumn<TEntity> : IDataTableColumn
    {
        /// <summary>
        /// Initializes the column with the default values.  Should match the defaults set in the Add() functions for the column collection.
        /// </summary>
        public DataTableColumn()
        {
            CanSort = true;
            Visible = true;
            CanSearch = true;
            Format = ColumnName.ToAccessor<TEntity>();
            Compiled = ColumnName.ToAccessor<TEntity>().Compile();
        }

       

        /// <summary>
        /// Indicates if the column is sortable.
        /// </summary>
        [DataMember(Name = "bSortable")]
        public bool CanSort { get; set; }

        /// <summary>
        /// Indicates if the column is searchable.
        /// </summary>
        public bool CanSearch { get; set; }

        /// <summary>
        /// The name of column.  This is usually set to the Property name in TEntity
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// A function to evaluate on every TEntity in the collection to be displayed to the user.
        /// </summary>
        public Expression<Func<TEntity, object?>> Format { get; set; }
        public Func<TEntity, object?> Compiled { get; set; }

        /// <summary>
        /// The header of the column.
        /// </summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>
        /// The CSS style class to be set on every element in the column.
        /// </summary>
        [DataMember(Name = "sClass")]
        public string Style { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the column is visible to the user.  The column may still be searched even when this is set to false.
        /// </summary>
        [DataMember(Name = "bVisible")]
        public bool Visible { get; set; }

        /// <summary>
        /// Used as a toggle switch when checked the search field will be populated with this value.
        /// </summary>
        public string Toggle { get; set; } = string.Empty;
    }

    /// <summary>
    /// A collection of DataTableColumn
    /// </summary>
    public class ColumnCollection<TEntity> : List<DataTableColumn<TEntity>>
    {
        public ColumnCollection()
        {
        }

        public ColumnCollection(IEnumerable<DataTableColumn<TEntity>> columns)
            : base(columns)
        {
        }

        public void Add<TValue>(Expression<Func<TEntity, TValue>> expression, 
            string? header = null, 
            Expression<Func<TEntity, object?>>? format = null, 
            bool canSort = true, 
            string? style = null, 
            bool visible = true, 
            string? toggle = null, 
            bool canSearch = true)
        {
            var item = expression.ToDictionary().Keys.FirstOrDefault() ?? expression.ToString();
            Add(item, header, format, canSort, style, visible, toggle, canSearch);
        }

        public void Add(string item, string? header = null, 
            Expression<Func<TEntity, object?>>? format = null, 
            bool canSort = true, 
            string? style = null, 
            bool visible = true, 
            string? toggle = null, 
            bool canSearch = true)
        {
            Add(new DataTableColumn<TEntity> { 
                ColumnName = item, 
                Format = format ?? (_ => null), 
                Header = header ?? string.Empty, 
                CanSort = canSort, 
                Style = style ?? string.Empty, 
                Visible = visible, 
                Toggle = toggle ?? string.Empty, 
                CanSearch = canSearch 
            });
        }
    }

    public interface IDataTablesInitializationModel
    {
        string ID { get; set; }
        string Url { get; set; }
        DataTablesDialogModelCollection Dialogs { get; set; }
        DataTablesDirectActionModelCollection DirectActions { get; set; }
        DataTablesAddModel Add { get; set; }
        int? DefaultCount { get; set; }
        List<DataTablesRequestModel.Sort> DefaultSorts { get; set; }
        Dictionary<string, object> TableAttributes { get; set; }

        int GetColumnIndex(string columnName);
        string GetColumnsJson();
        string GetSortsJson();
        string GetLanguageJson();

        bool ColumnIsValid(IDataTableColumn column);
        IEnumerable<IDataTableColumn> GetColumns();
    }

    /// <summary>
    /// Contains all the information needed to Render and continue display result for a DataTable
    /// </summary>
    public class DataTablesInitializationModel<TEntity> : IDataTablesInitializationModel
    {
        public string ID { get; set; } = string.Empty;
        public ColumnCollection<TEntity> Columns { get; set; } = [];
        public string Url { get; set; } = string.Empty;
        public DataTablesDialogModelCollection Dialogs { get; set; } = [];
        public DataTablesDirectActionModelCollection DirectActions { get; set; } = [];
        public DataTablesAddModel Add { get; set; } = new();
        public IQueryable<TEntity>? Source { get; set; }
        public int? DefaultCount { get; set; } = null;
        public List<DataTablesRequestModel.Sort> DefaultSorts { get; set; } = [];
        public Dictionary<string, object> TableAttributes { get; set; } = [];

        public IEnumerable<DataTableColumn<TEntity>> GetValidColumns()
        {
            // TODO: use metadata

            //var parameter = Expression.Parameter(typeof(TEntity), "m");

            //var validColumns = QueryableExtensions.GetMemberExpressions(Columns, parameter);
            //return validColumns.Select(x => x.Key);
            return [];
        }

        public int GetColumnIndex(string columnName)
        {
            return Columns.FindAll(n => n.Visible).FindIndex(n => n.ColumnName == columnName);
        }

        public string GetColumnsJson()
        {
            var validColumns = GetValidColumns();
            var serializer = new DataContractJsonSerializer(typeof(List<DataTableColumn<TEntity>>));

            foreach (var column in Columns)
            {
                if (!validColumns.Contains(column))
                {
                    column.CanSort = false;
                }
            }

            using var ms = new MemoryStream();
            serializer.WriteObject(ms, Columns);
            return Encoding.Default.GetString(ms.ToArray());
        }

        public IEnumerable<IDataTableColumn> GetColumns()
        {
            var validColumns = GetValidColumns();

            foreach (var column in Columns)
            {
                if (!validColumns.Contains(column))
                {
                    column.CanSort = false;
                }
            }
            return Columns;
        }

        public string GetSortsJson()
        {
            if (DefaultSorts == null)
                return string.Empty;

            var output = DefaultSorts.Select(x => new List<object> { x.iSortCol, x.sSortDir });
            return JsonSerializer.Serialize(output, JsonHelper.Default);
        }
        public string GetLanguageJson()
        {
            var output = new
            {
                sEmptyTable = "No data available in table.",
                sInfo = "Showing _START_ to _END_ of _TOTAL_ entries",
                sInfoEmpty = "Showing 0 to 0 of 0 entries",
                sInfoFiltered = "(filtered from _MAX_ total entries)",
                sInfoPostFix = "",
                sInfoThousands = ",",
                sLengthMenu = "Show _MENU_ entries",
                sLoadingRecords = "Loading...",
                sProcessing = "Processing...",
                sSearch = "Search:",
                sZeroRecords = "No matching records found",
                oPaginate = new
                {
                    sFirst = "First",
                    sLast = "Last",
                    sNext = "Next",
                    sPrevious = "Previous"
                },
                oAria = new
                {
                    sSortAscending = ": activate to sort column ascending",
                    sSortDescending = ": activate to sort column descending"
                }
            };

            return JsonSerializer.Serialize(output, JsonHelper.Default);
        }


        public bool ColumnIsValid(IDataTableColumn column)
        {
            var _validColumns = GetValidColumns();
            if (_validColumns.Contains(column))
                return true;
            return false;
        }
    }

    /// <summary>
    /// The set of properties for defining the look and behavior of functions that
    /// use a pop-up modal dialog.
    /// The dialog is populated by invoking a controller action that returns a
    /// partial view.
    /// </summary>
    public class DataTablesDialogModel
    {
        /// <summary>
        /// The name of the column to which the modal dialog is assigned.
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// The title of the modal dialog.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Width of the modal dialog.
        /// If not specified, it will be automatically calculated.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the modal dialog.
        /// If not specified, it will be automatically calculated.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Whether or not the dialog supports editing.
        /// If edit is supported, "save" and "cancel" buttons are provided.
        /// If edit is not supported, only a "close" button is provided.
        /// </summary>
        public bool Edit { get; set; }

        /// <summary>
        /// The text used to display on the save button
        /// </summary>
        public string SaveText { get; set; } = string.Empty;

    }

    /// <summary>
    /// A collection of modal dialog definitions.
    /// The definition of a table requires these be defined in a collection.
    /// </summary>
    public class DataTablesDialogModelCollection : List<DataTablesDialogModel>
    {
        public void Add(string columnName, string title, int width, int height, bool edit, string saveText = "")
        {
            Add(new DataTablesDialogModel { ColumnName = columnName, Edit = edit, Height = height, Title = title, Width = width, SaveText = saveText });
        }
    }

    /// <summary>
    /// The set of properties for defining the behavior of a "direct action" function.
    /// A direct action is one that makes an immediate AJAX invocation.
    /// </summary>
    public class DataTablesDirectActionModel
    {
        /// <summary>
        /// The name of the column to which the direct action is assigned.
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// The name of a Javascript function to invoke on successful invocation of
        /// the AJAX method.
        /// This function has the same signature as the "success" function defined by
        /// the JQuery $.ajax function.
        /// </summary>
        public string SuccessFunc { get; set; } = string.Empty;

        /// <summary>
        /// The name of a Javascript function to invoke when an error occurs invoking
        /// the AJAX method.
        /// This function has the same signature as the "error" function defined by
        /// the JQuery $.ajax function.
        /// </summary>
        public string ErrorFunc { get; set; } = string.Empty;
    }

    /// <summary>
    /// A collection of "direct action" definitions.
    /// The definition of a table requires these be defined in a collection.
    /// </summary>
    public class DataTablesDirectActionModelCollection : List<DataTablesDirectActionModel>
    {
    }

    /// <summary>
    /// The set of properties for defining the look and behavior of the "add" button
    /// which allows new entities to be added.
    /// </summary>
    public class DataTablesAddModel
    {
        /// <summary>
        /// The action to take when the button is clicked.
        /// Typically, this will be defined as an @Html.ActionLink.
        /// This action must return a partial view that will populate a modal dialog.
        /// </summary>
        public Func<IDataTablesInitializationModel, string>? Action { get; set; }

        /// <summary>
        /// The title for the modal dialog that displays the view for adding.
        /// If not specified, the <code>Text</code> property is used and the title
        /// of the dialog will be the same as button label.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Width of the modal dialog.
        /// If not specified, it will be automatically calculated.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the modal dialog.
        /// If not specified, it will be automatically calculated.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The text to display on the save button.
        /// </summary>
        public string SaveText { get; set; } = string.Empty;
    }

    public class DataTablesRequestModel
    {
        public string TableId { get; set; } = string.Empty;     // this is EPIC custom to support multiple tables per page
        public int DisplayStart { get; set; }
        public int DisplayLength { get; set; }
        public int Columns { get; set; }
        public int SortingCols { get; set; }
        public string Search { get; set; } = string.Empty;
        public bool Regex { get; set; }
        public int? Echo { get; set; }
        public List<ColumnFilter> ColumnFilters = [];
        public List<Sort> Sorts = [];

        public class ColumnFilter
        {
            public bool Searchable { get; set; }
            public string Search { get; set; } = string.Empty;
            public bool Regex { get; set; }
            public bool Sortable { get; set; }
            public string DataProp { get; set; } = string.Empty;
        }

        public class Sort
        {
            public int iSortCol;
            public string sSortDir = string.Empty;
        }
    }

    public class DataTablesResponseModel
    {
        private readonly List<RowData> rows;

        public DataTablesResponseModel()
        {
            rows = [];
        }

        public int Echo { get; set; }
        public int TotalRecords { get; set; }
        public int TotalDisplayRecords { get; set; }
        public RowData[] Data { get { return [.. rows]; } }

        public class RowData : Dictionary<string, string>
        {
            private int next = 0;

            public RowData()
            {
                SetRowId(string.Empty);
                SetRowClass(string.Empty);
            }

            public void SetRowId(string id)
            {
                this["DT_RowId"] = id;
            }

            public void SetRowClass(string clas)
            {
                this["DT_RowClass"] = clas;
            }

            public void PushColumn(string value)
            {
                this[next.ToString(CultureInfo.InvariantCulture)] = value;
                next++;
            }
        }

        public RowData NewRow()
        {
            RowData row = [];
            rows.Add(row);
            return row;
        }
    }
    
}
