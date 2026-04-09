using Extensions.PlayfulPlatforms.Windows;
using Extensions.PrometheusTypes;
using Interfacing.Services;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using static Extensions.PlayfulPlatforms.Windows.WinTrust;
using TypeExtensions = Extensions.PrometheusTypes.TypeExtensions;

namespace Atrium.Services;


public class TrustedLoader : ITrustProvider
{
    // GUID for the Action to verify a file using the Authenticode Policy Provider
    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
    private static readonly string MyThumbprint = "024eb7945944bb29c8fc16b7e83e885cda191fdf";
    //private static readonly X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromStore(MyThumbprint);
    private static string HomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string MyCertificatePath => Path.Combine(HomeDir, ".credentials\\my-code-signing.pfx");
    private static X509Certificate2 Mine => X509CertificateLoader.LoadCertificateFromFile(MyCertificatePath);
    private static readonly List<string> Whitelist = ["B1FB6C91198947FC"];

    public static bool VerifyDotNet(string filePath, string? expectedPublicKeyToken = null)
    {
        if (!File.Exists(filePath)) return false;

        expectedPublicKeyToken ??= WINTRUST_ACTION_GENERIC_VERIFY_V2.ToString();
        
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(filePath);
            byte[]? tokenBytes = assemblyName.GetPublicKeyToken();
            if (tokenBytes == null) return false;
            string actualToken = Convert.ToHexString(tokenBytes);

            if (!string.Equals(actualToken, expectedPublicKeyToken, StringComparison.OrdinalIgnoreCase))
                return false; // Not your assembly

            return true;
        }
        catch { return false; } // Not a .NET assembly at all
    }


    public static AssemblyName? VerifyStrongName(string filePath, string? thumbprint = null)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var name = AssemblyName.GetAssemblyName(filePath);
            var token = name.GetPublicKeyToken();

            if (token == null || token.Length == 0) return null;

            if(thumbprint != null
                && string.Equals(Convert.ToHexString(token), thumbprint, StringComparison.InvariantCultureIgnoreCase))
                return name;

            if (Whitelist.Contains(Convert.ToHexString(token)))
                return name;
            return null;
        }
        catch
        {
            // Likely a native DLL or corrupt file
            return null;
        }
    }

    /*
    public static bool VerifyCertificate(string filePath, string? thumbprint = null)
    {
        try
        {
            
        }
        catch
        {
            // Likely a native DLL or corrupt file
            return false;
        }
    }
    */



    public async Task<AssemblyInfo?> GetAssemblyInfoAsync(string filePath, string? expectedPublicKeyToken = null)
    {
        AssemblyInfo? meta = null;
        var level = LevelOfTrust.Meta;

        if (VerifyStrongName(filePath, expectedPublicKeyToken) is AssemblyName name)
        {
            meta = new AssemblyInfo(
                "Not Loaded",
                null,
                null,
                name.Name,
                LevelOfTrust.Published
                );
            level = LevelOfTrust.Published;
        }
        else
            return null;


        if (TypeExtensions.AllAssemblies
            .FirstOrDefault(a => string.Equals(a.Location ?? System.AppContext.BaseDirectory, filePath, StringComparison.InvariantCultureIgnoreCase))
            is Assembly ass)
            meta = new AssemblyInfo(
                ass.GetProduct(),
                ass.GetCompany(),
                ass.GetPublisher(),
                ass.GetPackage(),
                level
            );

        //if (VerifyCertificate(filePath, expectedPublicKeyToken))
        //    level = LevelOfTrust.Signed;
        //else
        //    return meta;

        if (expectedPublicKeyToken == null)
            level = LevelOfTrust.Mine;

        //if(meta == null)
        //    meta = MetadataReaderExtensions.GetAssemblyInfo(filePath);

        if (meta == null) return null;

        if (meta.IsMine())
            level = LevelOfTrust.Mine;

        expectedPublicKeyToken ??= WINTRUST_ACTION_GENERIC_VERIFY_V2.ToString();


        /*

        if (VerifyDotNet(filePath, expectedPublicKeyToken))
            level = LevelOfTrust.Verified;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            if (VerifyWindowsSignature(filePath, expectedPublicKeyToken))
                level = LevelOfTrust.Trusted;
        */

        return new AssemblyInfo(
            meta.Product,
            meta.Company,
            meta.Publisher,
            meta.Package,
            level
        );
    }
    



    public static bool VerifyWindowsSignature(string filePath, string? expectedPublicKeyToken = null)
    {
        var fileInfo = new WinTrustFileInfo(filePath);
        IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
        Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

        var trustData = new WinTrustData(fileInfoPtr);
        IntPtr trustDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(trustData));
        Marshal.StructureToPtr(trustData, trustDataPtr, false);

        expectedPublicKeyToken ??= WINTRUST_ACTION_GENERIC_VERIFY_V2.ToString();

        try
        {
            uint result = WinTrust.WinVerifyTrust(IntPtr.Zero, new Guid(expectedPublicKeyToken), trustDataPtr);
            return result == 0; // 0 = ERROR_SUCCESS
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
            Marshal.FreeHGlobal(trustDataPtr);
        }
    }

}

/*

public static class MetadataReaderExtensions
{
    public static AssemblyInfo? GetAssemblyInfo(string filePath)
    {
        try
        {
            if (filePath == null) return null;
            if (File.Exists(filePath) == false) return null;
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);
            var metadataReader = peReader.GetMetadataReader();

            return metadataReader.GetAssemblyInfo();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

*/