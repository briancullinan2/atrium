

namespace DataStore.Providers;

public class DataTablesRequestModel : Extensions.ForeignEntity.DataTablesRequestModel
{

    public static async ValueTask<DataTablesRequestModel?> BindAsync(HttpContext context)
    {
        // For DataTables, this usually comes via Form or QueryString
        var form = context.Request.HasFormContentType
            ? await context.Request.ReadFormAsync()
            : null;

        var query = context.Request.Query;

        // Helper to get value from either Form or Query
        string GetV(string key) => form?[key].FirstOrDefault() ?? query[key].FirstOrDefault() ?? string.Empty;

        var model = new DataTablesRequestModel
        {
            Echo = int.TryParse(GetV("sEcho"), out var echo) ? echo : null,
            Columns = int.TryParse(GetV("iColumns"), out var cols) ? cols : 0,
            SortingCols = int.TryParse(GetV("iSortingCols"), out var sCols) ? sCols : 0,
            // ... map other base properties here
        };

        if (model.Echo == null) return null;

        // The "General Use" loop
        for (int i = 0; i < model.Columns; i++)
        {
            if (string.IsNullOrEmpty(GetV($"mDataProp_{i}"))) break;

            model.ColumnFilters.Add(new DataTablesRequestModel.ColumnFilter
            {
                Searchable = GetV($"bSearchable_{i}") == "true",
                Search = GetV($"sSearch_{i}"),
                Regex = GetV($"bRegex_{i}") == "true",
                Sortable = GetV($"bSortable_{i}") == "true",
                DataProp = GetV($"mDataProp_{i}")
            });
        }

        for (int i = 0; i < model.SortingCols; i++)
        {
            if (string.IsNullOrEmpty(GetV($"iSortCol_{i}"))) break;

            model.Sorts.Add(new DataTablesRequestModel.Sort
            {
                iSortCol = int.TryParse(GetV($"iSortCol_{i}"), out var c) ? c : 0,
                sSortDir = GetV($"sSortDir_{i}")
            });
        }

        return model;
    }
}
