using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Pinpoint.Core
{
    public class PluginEngine
    {
        public List<IPlugin> Plugins { get; } = new List<IPlugin>();

        public List<IPluginListener<IPlugin, object>> Listeners { get; } = new List<IPluginListener<IPlugin, object>>();

        public void AddPlugin(IPlugin plugin)
        {
            var prevSettings = AppSettings.GetListAs<PluginMeta>("plugins");

            var pluginLoader = new PluginLoader();
            var x = pluginLoader.GetAvailablePlugins();

            if (!Plugins.Contains(plugin))
            {
                plugin.Load();

                var pluginName = plugin.Meta.Name;
                if (prevSettings.Any(m => m.Name.Equals(pluginName)))
                {
                    plugin.Meta = prevSettings.First(m => m.Name.Equals(pluginName));
                }

                Plugins.Add(plugin);

                // Ensure order of plugin execution is correct
                Plugins.Sort();

                Listeners.ForEach(listener => listener.PluginChange_Added(this, plugin, null));
            }
        }

        public void RemovePlugin(IPlugin plugin)
        {
            if (Plugins.Contains(plugin))
            {
                plugin.Unload();
                Plugins.Remove(plugin);
                Listeners.ForEach(listener => listener.PluginChange_Removed(this, plugin, null));
            }
        }

        public T Plugin<T>() where T : IPlugin
        {
            return Plugins.Where(p => p is T).Cast<T>().FirstOrDefault();
        }

        public async IAsyncEnumerable<AbstractQueryResult> Process(Query query, [EnumeratorCancellation] CancellationToken ct)
        {
            var numResults = 0;

            foreach (var plugin in Plugins.Where(p => p.Meta.Enabled))
            {
                if (numResults >= 30)
                {
                    yield break;
                }

                if (await plugin.Activate(query))
                {
                    await foreach (var result in plugin.Process(query).WithCancellation(ct))
                    {
                        numResults++;
                        yield return result;
                    }
                }
            }
        }
    }

    public class PluginLoader
    {
        public IEnumerable<IPlugin> GetAvailablePlugins()
        {
            EnsurePluginDirectoryExists();

            foreach (var filePath in Directory.EnumerateFiles(AppConstants.PluginFolderPath))
            {
                if (!filePath.EndsWith(".dll")) continue;

                var assembly = Assembly.LoadFrom(filePath);

                var pluginType = assembly.ExportedTypes.FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t));

                if(pluginType == null) continue;

                var plugin = (IPlugin) Activator.CreateInstance(pluginType);

                yield return plugin;
            }
        }

        private static void EnsurePluginDirectoryExists()
        {
            var exists = Directory.Exists(AppConstants.PluginFolderPath);

            if (!exists)
            {
                Directory.CreateDirectory(AppConstants.PluginFolderPath);
            }
        }

    }
}
