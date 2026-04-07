using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hosting.Services
{
    public partial class FileManager
    {

#if !BROWSER


        public async Task<string?> OpenFile(string file)
        {
            string path = Path.Combine("wwwroot", file);

            if (await FileSystem.AppPackageFileExistsAsync(path))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(path);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }

            return null;
        }
#else

        public async Task<string?> OpenFile(string file)
        {
            var result = Http.GetStringAsync(file);
            if (result == null) return null;
            return await result;
        }


#endif


#if WINDOWS


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
}
