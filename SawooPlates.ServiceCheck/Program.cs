using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

var dev = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"XIVLauncher\addon\Hooks\dev");
AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var p = Path.Combine(dev, name.Name + ".dll");
    return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
};

var services = new ServiceCollection();
// every Dalamud service the plugin constructor registers, as placeholders
var dalamud = typeof(Dalamud.Plugin.IDalamudPluginInterface).Assembly;
foreach (var name in new[] { "Dalamud.Plugin.IDalamudPluginInterface", "Dalamud.Plugin.Services.IPluginLog", "Dalamud.Plugin.Services.IFramework",
    "Dalamud.Plugin.Services.IChatGui", "Dalamud.Plugin.Services.IClientState", "Dalamud.Plugin.Services.IDataManager", "Dalamud.Plugin.Services.ITextureProvider",
    "Dalamud.Plugin.Services.IGameInteropProvider", "Dalamud.Plugin.Services.IAddonLifecycle", "Dalamud.Plugin.Services.IAgentLifecycle",
    "Dalamud.Plugin.Services.INotificationManager", "Dalamud.Plugin.Services.IPlayerState", "Dalamud.Plugin.Services.IContextMenu" })
{
    var t = dalamud.GetType(name) ?? throw new Exception("missing type " + name);
    services.AddSingleton(t, _ => throw new NotSupportedException());
}
services.AddSingleton(typeof(Dalamud.Interface.Windowing.WindowSystem), _ => throw new NotSupportedException());

var method = typeof(SawooPlates.PluginServices).GetMethod("AddPluginServices")!;
method.Invoke(null, [services]);

try
{
    using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    Console.WriteLine($"OK: {services.Count} registrations resolve");
}
catch (AggregateException ex)
{
    foreach (var e in ex.InnerExceptions) Console.WriteLine("FAIL: " + e.Message);
    Environment.Exit(1);
}
