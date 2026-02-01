using GriniClean.Core.Models;
using GriniClean.Infrastructure.FileSystem;
using GriniClean.Infrastructure.OS;
using GriniClean.Modules.Cache.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace GriniClean;

public static class Program
{
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            b.SetMinimumLevel(LogLevel.Information);
        });

        // Infrastructure
        services.AddSingleton<IFileSystem, OsFileSystem>();
        services.AddSingleton<IProcessService, MacProcessService>();
        services.AddSingleton<ITrashService, MacTrashService>();
        services.AddSingleton<IUserPaths, MacUserPaths>();

        // Cache module
        services.AddSingleton<ICacheScanner, MacCacheScanner>();
        services.AddSingleton<ICacheCleaner, MacCacheCleaner>();

        // Commands
        services.AddSingleton<CacheScanCommand>();
        services.AddSingleton<CacheCleanCommand>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);

        app.Configure(config =>
        {
            config.SetApplicationName("gc");

            config.AddCommand<CacheScanCommand>("cache-scan")
                .WithDescription("Scan safe user cache locations (no system directories).");
            config.AddCommand<CacheCleanCommand>("cache-clean")
                .WithDescription("Interactively choose cache targets and move them to Trash (Trash-first).");
        });

        return app.Run(args);
    }
}

// Simple Spectre DI registrar
internal sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(builder.BuildServiceProvider());
    public void Register(Type service, Type implementation) => builder.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object implementation) => builder.AddSingleton(service, implementation);
    public void RegisterLazy(Type service, Func<object> factory) => builder.AddSingleton(service, _ => factory());

    private sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
    {
        public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);
        public void Dispose() { if (provider is IDisposable d) d.Dispose(); }
    }
}
