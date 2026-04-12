using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Interfacing.Services;

public interface IHasBuilder
{
    static abstract void BuildServices(IServiceCollection services, string? key = null);
}

public interface IHasService
{
    IServiceProvider Services { get; }
}

public interface IHasCurrent<T>
{
    static abstract T? Current { get; }
}

public interface IHasPlugins
{
    // this is the name of the setting indicating its installed or null for not
    //   this is used to shortcut the enabled process after the first load
    // should return a Task<string?>
    static abstract Delegate Installed { get; }
    // this is what the service manager should automatically add to DI if enabled after being checked for installed 
    // should return a Dictionary<Type, (string Name, string Icon)>?
    static abstract Delegate Plugins { get; }
}

public interface IHasFeatures
{
    // this is a list of fully qualified type names of enabled features for short reference
    // should return a Task<List<string>?>
    static abstract Delegate Installed { get; }
    // this is what the UX should display after checking installed
    // should return a Dictionary<Type, (string Name, string Icon)>?
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

public record PluginContract(
    string Title,
    string InstallPath,
    bool IsTrusted,
    AssemblyInfo Metadata);

public class Dumbass { }

public interface ITrustProvider
{
    Task<AssemblyInfo?> GetAssemblyInfoAsync(string filepath, string? pubKey = null);
    Task<LevelOfTrust?> GetTrustedAsync(string filepath, string? pubKey = null);
    event Action<PluginContract> OnAssemblyLoaded;
    event Action OnSettled;
    bool IsBootstrapping { get; }
    List<string> RequiredAssemblies { get; }
    [RequiresAssemblyFiles]
    Dictionary<string, Assembly> LoadedAssemblies { get; }
    Dictionary<string, bool> EnabledAssemblies { get; }
    void Enable(string ass);
    void Disable(string ass);
    Dictionary<string, List<string>> DependedAssemblies { get; }
    ConcurrentDictionary<string, PluginContract> DiscoveredStatus { get; }
}

public record AssemblyInfo(string? Product, string? Company, string? Publisher, string? Package, LevelOfTrust TrustLevel = LevelOfTrust.None);

public enum LevelOfTrust : int
{
    None = 0,
    Meta = 1, // title from a known publisher
    Required = 2,
    Published = 3, // strongly named
    Signed = 4, // strong name or authenticode signature from a trusted authority
    Mine = 5, // title from a known publisher and matches an assembly already loaded into the app domain
    Verified = 6, // verified through windows signing
    Trusted = 7, // cert is already in user store
    Full = 8 // manually marked as trusted by user
}

public interface IHasForms
{

}


