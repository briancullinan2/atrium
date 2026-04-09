using Interfacing.Services;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;

#if WINDOWS
using System.Runtime.InteropServices;
using Atrium.Platforms.Windows;
#endif

namespace Atrium.Services;


public class TrustedLoader : ITrustProvider
{
    // GUID for the Action to verify a file using the Authenticode Policy Provider
#if WINDOWS
    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
#endif
    //private static readonly string MyThumbprint = "024eb7945944bb29c8fc16b7e83e885cda191fdf";
    //private static readonly X509Certificate2 cert = X509CertificateLoader.LoadCertificateFromStore(MyThumbprint);
    //private static string HomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    //private static string MyCertificatePath => Path.Combine(HomeDir, ".credentials\\my-code-signing.pfx");
    //private static X509Certificate2 Mine => X509CertificateLoader.LoadCertificateFromFile(MyCertificatePath);
    private static readonly List<string> Whitelist = ["B1FB6C91198947FC"];


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


    public async Task<LevelOfTrust?> GetTrustedAsync(string filePath, string? expectedPublicKeyToken = null)
    {
        LevelOfTrust level;
        if (VerifyStrongName(filePath, expectedPublicKeyToken) != null)
            level = LevelOfTrust.Published;
        else
            return null;

        // TODO: fix flow for this
        if (expectedPublicKeyToken == null)
            level = LevelOfTrust.Mine;

#if WINDOWS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            if (VerifyWindowsSignature(filePath, expectedPublicKeyToken))
                level = LevelOfTrust.Verified;
#endif


        //if (VerifyCertificate(filePath, expectedPublicKeyToken))
        //    level = LevelOfTrust.Signed;

        return level;
    }

    public async Task<AssemblyInfo?> GetAssemblyInfoAsync(string filePath, string? expectedPublicKeyToken = null)
    {
        var level = await GetTrustedAsync(filePath);

        if(level == null)
            return new AssemblyInfo(
                "Not Trustable",
                null,
                null,
                Path.GetFileNameWithoutExtension(filePath),
                LevelOfTrust.None
            );

        if (level < LevelOfTrust.Published)
            return new AssemblyInfo(
                "Not Loaded",
                null,
                null,
                Path.GetFileNameWithoutExtension(filePath),
                level.Value
            );

        // TODO: temporary
        /*if (TypeExtensions.AllAssemblies
            .FirstOrDefault(a => string.Equals(a.Location ?? System.AppContext.BaseDirectory, filePath, StringComparison.InvariantCultureIgnoreCase))
            is Assembly ass)
            meta = new AssemblyInfo(
                ass.GetProduct(),
                ass.GetCompany(),
                ass.GetPublisher(),
                ass.GetPackage(),
                level.Value
            );
        else*/

        AssemblyInfo? meta = MetadataReaderExtensions.GetAssemblyInfo(filePath);
        if (meta == null) return new AssemblyInfo(
            "No Metadata",
            "",
            "",
            Path.GetFileNameWithoutExtension(filePath),
            level.Value
        );

        if (meta.IsMine())
            level = LevelOfTrust.Mine;


        return new AssemblyInfo(
            meta.Product,
            meta.Company,
            meta.Publisher,
            meta.Package,
            level.Value
        );
    }


#if WINDOWS
    public static bool VerifyWindowsSignature(string filePath, string? expectedPublicKeyToken = null)
    {
        var fileInfo = new WinTrust.WinTrustFileInfo(filePath);
        IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
        Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

        var trustData = new WinTrust.WinTrustData(fileInfoPtr);
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
#endif

}


internal static class MetadataReaderExtensions
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

            return metadataReader.MetaAttributeNameValueMatch();
        }
        catch (Exception)
        {
            return null;
        }
    }


    public static AssemblyInfo MetaAttributeNameValueMatch(this MetadataReader? mr)
    {
        if (mr == null) return new AssemblyInfo(null, null, null, null);

        string? product = null, company = null, publisher = null, package = null, authors = null;


        foreach (var handle in mr.CustomAttributes)
        {
            var attr = mr.GetCustomAttribute(handle);
            var (name, value) = DecodeAttribute(mr, attr);

            switch (name)
            {
                case "AssemblyProductAttribute":
                    product = value;
                    break;
                case "AssemblyCompanyAttribute":
                    company = value;
                    break;
                case "AssemblyMetadataAttribute":
                    // Metadata attributes are Key/Value pairs. 
                    // Ensure DecodeAttribute handles the Key|Value format correctly.
                    var parts = value.Split('|');
                    if (parts.Length < 2) break;

                    var key = parts[0];
                    var val = parts[1];

                    if (key == "PublisherName") publisher = val;
                    if (key == "PackageName") package = val;
                    // Catch "Authors" if you stored it in metadata
                    if (key == "Authors") authors = val;
                    if (key == "CompanyName" && company == null) company = val;
                    break;
            }
        }
        return new AssemblyInfo(product, company, publisher + authors, package);
    }



    private static (string Name, string Value) DecodeAttribute(MetadataReader mr, CustomAttribute attr)
    {
        string name = "";
        if (attr.Constructor.Kind == HandleKind.MemberReference)
        {
            var mrf = mr.GetMemberReference((MemberReferenceHandle)attr.Constructor);
            if (mrf.Parent.Kind == HandleKind.TypeReference)
            {
                var tr = mr.GetTypeReference((TypeReferenceHandle)mrf.Parent);
                name = mr.GetString(tr.Name);
            }
        }

        // --- DUCK OUT EARLY ---
        // List only the attributes we actually care about in the switch/extension methods
        if (name != "AssemblyProductAttribute" &&
            name != "AssemblyCompanyAttribute" &&
            name != "AssemblyMetadataAttribute" &&
            name != "AssemblyTitleAttribute" &&
            name != "AssemblyDescriptionAttribute")
        {
            return (name, ""); // Return the name so the loop knows we saw it, but skip the value
        }

        var reader = mr.GetBlobReader(attr.Value);

        // Attributes start with a 0x0001 prolog
        if (reader.Length < 4 || reader.ReadUInt16() != 1) return (name, "");

        string val = "";
        try
        {
            // Read the first argument
            val = reader.ReadSerializedString() ?? string.Empty;

            if (name == "AssemblyMetadataAttribute")
            {
                // Read the second argument (the Value in the Key/Value pair)
                string key = val;
                string data = reader.ReadSerializedString() ?? string.Empty;
                val = $"{key}|{data}";
            }
        }
        catch { /* Metadata is malformed or not a simple string attribute */ }


        return (name, val);
    }


    private static readonly Assembly entry;
    private static readonly string? entryDirectory;
    private static readonly string? product;
    private static readonly string? package;
    private static readonly string? company;
    private static readonly string? publisher;

    static MetadataReaderExtensions()
    {
        entry ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        entryDirectory ??= Path.GetDirectoryName(AppContext.BaseDirectory);
        product ??= GetProduct(entry);
        package ??= GetPackage(entry);
        publisher ??= GetPublisher(entry);
        company ??= GetCompany(entry);

    }


    public static bool IsMine(this AssemblyInfo ass)
    {

        if (entryDirectory == null) return false;


        if ((product != null && string.Equals(product, ass.Product, StringComparison.InvariantCultureIgnoreCase))

            || (package != null && string.Equals(package, ass.Package, StringComparison.InvariantCultureIgnoreCase))

            || (publisher != null && string.Equals(publisher, ass.Publisher, StringComparison.InvariantCultureIgnoreCase))

            || (company != null && string.Equals(company, ass.Company, StringComparison.InvariantCultureIgnoreCase))
        ) return true;

        return false;

    }



    public static string? GetProduct(this Assembly entry)
        => entry.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? entry.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

    public static string? GetPackage(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "PackageName" || attr.Key == "PackageId")?.Value
        ?? entry.GetName().Name; // Fallback to the actual DLL name

    public static string? GetPublisher(this Assembly entry)
        => entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "PublisherName" || attr.Key == "Authors" || attr.Key == "Owner")?.Value
        ?? entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

    public static string? GetCompany(this Assembly entry)
        => entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
        ?? entry.GetCustomAttributes<AssemblyMetadataAttribute>()
        ?.FirstOrDefault(attr => attr.Key == "CompanyName" || attr.Key == "Organization")?.Value;



}
