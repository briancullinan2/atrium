using System.Runtime.InteropServices;

namespace Extensions.PlayfulPlatforms.MacCatalyst
{
#if APPLE
    public static class Security
    {
        [LibraryImport("/System/Library/Frameworks/Security.framework/Security")]
        public static partial int SecStaticCodeCheckValidity(IntPtr code, uint flags, IntPtr requirement);
    }
#endif
}