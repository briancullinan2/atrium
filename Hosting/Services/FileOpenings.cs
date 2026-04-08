#if !BROWSER
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using static System.Net.WebRequestMethods;
#endif

namespace Hosting.Services;

public partial class FileManager
{

#if !BROWSER


    public async Task<string?> OpenFile(string file)
    {
        string baseDir = AppContext.BaseDirectory;

        string fullPath = Path.GetFullPath(Path.Combine(baseDir, file));

        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            // Path attempted to escape the app directory
            return null;
        }

        string packagePath = Path.Combine(baseDir, "wwwroot", file);

        if (File.Exists(file))
        {
            return File.ReadAllText(fullPath);
        }
        throw new InvalidOperationException("File not found locally: " + file);
    }

    public async Task<string?> OpenFileAsync(string file)
    {
        string baseDir = AppContext.BaseDirectory;

        string fullPath = Path.GetFullPath(Path.Combine(baseDir, file));

        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            // Path attempted to escape the app directory
            return null;
        }

        string packagePath = Path.Combine(baseDir, "wwwroot", file);

        if (await FileSystem.AppPackageFileExistsAsync(packagePath))
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(packagePath);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        else if (File.Exists(file))
        {
            return await File.ReadAllTextAsync(fullPath);
        }
        else
        {
            return await Http.GetStringAsync(file);
        }


    }
#else

    public async Task<string?> OpenFile(string file)
    {
        var result = Http.GetStringAsync(file);
        if (result == null) return null;
        return await result;
    }


#endif


#if BROWSER


    public async Task OpenFileDialog()
    {
        throw new InvalidOperationException("File dialogs are not supported in browser environments. Please use the file input element instead.");
    }
#else

    public async Task OpenFileDialog()
    {
        try
        {
            // This calls the native Windows picker, bypassing WebView2 bugs
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Anki Package",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                    { DevicePlatform.WinUI, (string[])[".apkg", ".zip"] }
                }) // or custom types
            });

            if (result != null)
            {
                // You get the ABSOLUTE path immediately! 
                // No more "browser sandbox" stream restrictions.
                _ = UploadFile(System.IO.File.OpenRead(result.FullPath), result.FullPath);
            }
        }
        catch (Exception)
        {
            // Handle cancel or permission issues
        }
    }


#endif


}
