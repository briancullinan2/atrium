using DataLayer.Entities;
using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FlashCard.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stream = System.IO.Stream;
#if WINDOWS
using Atrium.Platforms.Windows;
#endif

namespace Atrium.Services
{
    internal class FileManager : IFileManager
    {
        public event Action<DataLayer.Entities.File?>? OnFileUploaded;
        public event Action<bool>? OnFileDragging;
        internal static IServiceProvider? _services;

        public static string UploadDirectory = Path.Combine(AppContext.BaseDirectory, "Uploads");

        static FileManager()
        {

            if (!Directory.Exists(UploadDirectory))
            {
                Directory.CreateDirectory(UploadDirectory);
            }
        }


        public async Task<DataLayer.Entities.File?> UploadFile(string localPath)
        {
            using var localStream = System.IO.File.OpenRead(localPath);
            return await UploadFile(localStream, localPath);
        }


        // TODO: generalize not just for anki and add a parameter like string source = "Uploads"
        //   so any implementer can designate themselves as the source of the data

        public async Task<DataLayer.Entities.File?> UploadFile(Stream localStream, string localPath, string? source = "Uploads")
        {

            if (_services == null)
            {
                throw new InvalidOperationException("No service provider.");
            }

            var savePath = Path.Combine(UploadDirectory, Path.GetFileName(localPath).ToSafe());
            using var fileStream = System.IO.File.Create(savePath);
            await localStream.CopyToAsync(fileStream);
            // TODO: store in database and return File entity?
            fileStream.Close();
            localStream.Close();


            var persistentStore = _services.GetRequiredService<IDbContextFactory<DataLayer.PersistentStorage>>();
            using var fileContext = persistentStore?.CreateDbContext();
            try
            {
                ProxyEntity<DataLayer.Entities.File> file = DataLayer.Entities.Entity.Wrap(new DataLayer.Entities.File()
                {
                    Filename = savePath,
                    Source = source // TODO: fill in from nav or parameter or something
                }, _services);
                file.Save();

                OnFileUploaded?.Invoke(file._target);
                return file._target;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw new Exception("throwing up", ex);
            }
        }

#if WINDOWS
        //[HttpPost("upload")]
        public static async Task OnUploadFile(HttpContext context)
        {
            try
            {
                if(_services == null)
                {
                    throw new InvalidOperationException("No service provider.");
                }

                if (!context.Request.HasFormContentType || !context.Request.Form.Files.Any())
                {
                    throw new InvalidOperationException("No files provided.");
                }

                var first = true;
                foreach (var file in context.Request.Form.Files)
                {
                    // Accessing the stream directly from the request
                    using var stream = file.OpenReadStream();

                    // Example: Save to disk in Arizona-based storage
                    using (var scope = _services.CreateScope())
                    {
                        var manager = scope.ServiceProvider.GetRequiredService<IFileManager>();
                        var databaseFile = await manager.UploadFile(stream, file.FileName);

                        if (!first) continue;
                        first = false;

                        context.Response.ContentType = "application/json";
                        var json = JsonSerializer.Serialize(databaseFile, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                        });
                        await context.Response.WriteAsync(json);
                        await context.Response.Body.FlushAsync();
                        await context.Response.CompleteAsync();
                    }

                }
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(ex.Message, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles // Important for EF Entities
                });
                await context.Response.WriteAsync(json);
            }
        }

#endif

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
                    using (var scope = _services?.CreateScope())
                    {
                        var manager = scope?.ServiceProvider.GetRequiredService<IFileManager>();
                        _ = manager?.UploadFile(System.IO.File.OpenRead(result.FullPath), result.FullPath);
                    }
                }
            }
            catch (Exception)
            {
                // Handle cancel or permission issues
            }
        }

        public async Task SetDragging(bool dragging)
        {
            OnFileDragging?.Invoke(dragging);
        }

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


#if WINDOWS
        private static User32.WndProcDelegate? _wndProc; // Keep static to prevent GC
        private static nint _oldWndProc;
        private static bool _isFileDragging;

        internal static void InitializeWndProc(Microsoft.Maui.Handlers.IWindowHandler h)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(h.PlatformView);
            // 0x0233 is WM_DROPFILES
            // 0x0049 is WM_COPYGLOBALDATA (Crucial for the "No-Drop" cursor fix)
            User32.AllowDrops(hwnd);
            Shell32.DragAcceptFiles(hwnd, 1);
            _wndProc = MyWndProc; // Simplified assignment
            _oldWndProc = User32.SetWindowLongPtr(hwnd, -4,  System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc));

        }

        private static nint MyWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == Shell32.WM_DROPFILES) // WM_DROPFILES
            {
                _isFileDragging = false;
                HandleNativeDrop(wParam);
                return nint.Zero;
            }

            if (msg == 0x0040) // WM_NOTIFY
            {
                // Internal WinUI3 / WebView2 notification handling could go here
                User32.AllowDrops(hWnd);
            }

            if (msg == 0x0047) // WM_WINDOWPOSCHANGED or similar "hover" messages
            {
                // This is where you would normally tell the shell to change the cursor
                User32.AllowDrops(hWnd);
            }

            if(msg == Shell32.WM_CAPTURECHANGED)
            {
                if(!_isFileDragging)
                {
                    _isFileDragging = true;
                    using(var scope = _services?.CreateScope())
                    {
                        // Notify the front end UI of the upload
                        var manager = scope?.ServiceProvider.GetRequiredService<IFileManager>();
                        manager?.SetDragging(true);
                    }
                }
            }

            /*
            if (msg == Shell32.WM_SETCURSOR)
            {
                // High word of lParam is the mouse message that triggered it
                ushort mouseMsg = (ushort)((((ulong)lParam) >> 16) & 0xffff);
        
                if (mouseMsg == Shell32.WM_MOUSEMOVE)
                {
                    // If we get SETCURSOR + MOUSEMOVE but our app doesn't have 
                    // the mouse 'captured', it's almost certainly an external DRAG.
                    if(!_isFileDragging)
                    {
                        _isFileDragging = true;
                        using(var scope = _services?.CreateScope())
                        {
                            // Notify the front end UI of the upload
                            var manager = scope?.ServiceProvider.GetRequiredService<IFileManager>();
                            manager?.SetDragging(true);
                        }

                    }
                }
            }
            */

            return User32.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }


        private static void HandleNativeDrop(nint hDrop)
        {
            // Get count of dropped files
            uint fileCount = Shell32.DragQueryFile(hDrop, 0xFFFFFFFF, nint.Zero, 0);

            using(var scope = _services?.CreateScope())
            {
                // Notify the front end UI of the upload
                var manager = scope?.ServiceProvider.GetRequiredService<IFileManager>();
                manager?.SetDragging(false);
                for (uint i = 0; i < fileCount; i++)
                {
                    // 1. Get required length (returns length without null terminator)
                    uint length = Shell32.DragQueryFile(hDrop, i, nint.Zero, 0) + 1;

                    // 2. Allocate buffer and pin it
                    char[] buffer = new char[length];
                    unsafe
                    {
                        fixed (char* pBuffer = buffer)
                        {
                            // 3. Fill the buffer
                            Shell32.DragQueryFile(hDrop, i, (nint)pBuffer, length);
                        }
                    }

                    // 4. Convert to C# string
                    string filePath = new string(buffer).TrimEnd('\0');
                
                    manager?.UploadFile(filePath);
                }
            }

            Shell32.DragFinish(hDrop);
        }
#endif
    }
}
