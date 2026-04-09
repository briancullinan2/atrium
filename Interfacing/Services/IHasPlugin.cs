using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Interfacing.Services;

public interface IHasService
{
    static abstract IServiceProvider Services { get; }
}

public interface IHasService<T> : IHasService
{
    static abstract T? Current { get; }
}

public interface IHasPlugins
{
    // this is the name of the setting indicating its installed or null for not
    // should return a Task<string?> 
    static abstract Delegate Installed { get; }
    // this is what the service manager should automatically add to DI if enabled after being checked for installed 
    // should return a List<Type>?
    static abstract Delegate Plugins { get; }
}

public interface IHasFeatures
{
    // this is a list of fully qualified type names of enabled features for short reference
    // should return a Task<List<string>?>
    static abstract Delegate Installed { get; }
    // this is what the UX should display after checking installed
    // should return a Dictionary<Type, string>?
    static abstract Delegate DisplayNames { get; }
}

public interface IHasSettings
{
    static abstract Delegate ShowSettings { get; }
    static abstract Delegate SettingInsert { get; }
}

public interface IHasStatus
{
    static abstract Delegate ShowStatus { get; }
    static abstract Delegate StatusInsert { get; }
}

public interface IHasHome
{
    static abstract Delegate ShowHome { get; }
    static abstract Delegate HomeInsert { get; }
}

public interface IHasPageContext
{
    static abstract Delegate ShowPageContext { get; }
    static abstract Delegate PageContextInsert { get; }
}

public record PluginContract(
    string Title,
    string InstallPath,
    bool IsTrusted,
    AssemblyInfo Metadata,
    bool IsActive);

public interface ITrustProvider
{
    Task<AssemblyInfo?> GetAssemblyInfoAsync(string filepath, string? pubKey = null);
    Task<LevelOfTrust?> GetTrustedAsync(string filepath, string? pubKey = null);
    event Action<PluginContract> OnAssemblyLoaded;
    bool IsBootstrapping { get; }
    ConcurrentDictionary<string, PluginContract> DiscoveredStatus { get; }

}

public record AssemblyInfo(string? Product, string? Company, string? Publisher, string? Package, LevelOfTrust TrustLevel = LevelOfTrust.None);

public enum LevelOfTrust : int
{
    None = 0,
    Meta = 1, // title from a known publisher
    Published = 2, // strongly named
    Signed = 3, // strong name or authenticode signature from a trusted authority
    Mine = 4, // title from a known publisher and matches an assembly already loaded into the app domain
    Verified = 5, // verified through windows signing
    Trusted = 6, // cert is already in user store
    Full = 7 // manually marked as trusted by user
}

