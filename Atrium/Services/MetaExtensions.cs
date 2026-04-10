using Interfacing.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Atrium.Services;


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

    [RequiresAssemblyFiles("Calls System.Reflection.Assembly.Location")]
    public static bool IsMine(this Assembly ass)
    {

        if (entryDirectory == null) return false;

        if (!string.Equals(ass.Location[..Math.Min(entryDirectory.Length, ass.Location.Length)],
                        entryDirectory, StringComparison.InvariantCultureIgnoreCase)) return false;

        if ((product != null && string.Equals(product, GetProduct(ass), StringComparison.InvariantCultureIgnoreCase))

            || (package != null && string.Equals(package, GetPackage(ass), StringComparison.InvariantCultureIgnoreCase))

            || (publisher != null && string.Equals(publisher, GetPublisher(ass), StringComparison.InvariantCultureIgnoreCase))

            || (company != null && string.Equals(company, GetCompany(ass), StringComparison.InvariantCultureIgnoreCase))
        )
            return true;

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


    public static AssemblyInfo GetAssemblyInfo(this Assembly? entry)
    {
        if (entry == null) return new AssemblyInfo(null, null, null, null);
        var product = GetProduct(entry);
        var package = GetPackage(entry);
        var publisher = GetPublisher(entry);
        var company = GetCompany(entry);
        return new AssemblyInfo(product, company, publisher, package);
    }

    public static AssemblyInfo GetAssemblyInfo(this Type? entry)
    {
        return entry?.Assembly.GetAssemblyInfo() ?? new AssemblyInfo(null, null, null, null);
    }

    public static object? InvokeService(this Delegate? myDelegate, IServiceProvider service, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        return myDelegate.Method.InvokeService(service, myDelegate.Target, args);
    }

    public static object? InvokeService(this MethodInfo? myDelegate, IServiceProvider service, object? thisObject = null, params object?[]? args)
    {
        if (myDelegate == null) throw new InvalidOperationException("MethodInfo cannot be null.");
        var formFactor = service.GetService(typeof(IFormFactor)) as IFormFactor;
        var parameters = myDelegate.GetParameters();
        var parameterValues = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var realType = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
            // TODO: add more special service injection handlers
            //if (parameters[i].ParameterType == typeof(Type) && string.Equals(parameters[i].Name, "routeControl"))
            //{
            //    var nav = service.GetRequiredService<NavigationManager>();
            //    parameterValues[i] = IdentifyNavigation(nav.Uri).ComponentType;
            //}
            //else 
            if (args?.ElementAtOrDefault(i) == null && Nullable.GetUnderlyingType(parameters[i].ParameterType) != null)
            {
                parameterValues[i] = null;
            }
            // TODO: find a way to match parameter names to a dictionary passed in or query params?
            else if (args?.ElementAtOrDefault(i) is object obj
                && realType.IsAssignableFrom(obj.GetType()))
            {
                parameterValues[i] = Convert.ChangeType(obj, realType);
            }
            else if (args?.FirstOrDefault(a => realType.IsAssignableFrom(a?.GetType()) == true) is object obj2)
            {
                parameterValues[i] = Convert.ChangeType(obj2, realType);
            }
            else if (!string.IsNullOrEmpty(parameters[i].Name)
                && formFactor?.QueryParameters?.ContainsKey(parameters[i].Name!) is object queryParameter)
            {
                parameterValues[i] = Convert.ChangeType(queryParameter, realType);
            }
            else
            {
                parameterValues[i] = service.GetService(realType);
            }
        }

        if (thisObject != null && !myDelegate.IsStatic)
        {
            return myDelegate.Invoke(thisObject, parameterValues);
        }
        return myDelegate.Invoke(null, thisObject != null ? [thisObject, .. parameterValues] : parameterValues);
    }
}
