﻿using Microsoft.Extensions.Logging;
using Obsidian.API.Plugins;
using Obsidian.Plugins.ServiceProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Obsidian.Plugins
{
    public class PluginContainer
    {
        public PluginBase Plugin { get; }
        public PluginInfo Info { get; }
        public string Source { get; internal set; }
        public Assembly Assembly { get; }
        public AssemblyLoadContext LoadContext { get; }
        public string ClassName { get; }

        private PluginPermissions _permissions;
        public PluginPermissions Permissions { get => _permissions; set => UpdatePermissions(value); }
        public PluginPermissions NeedsPermissions { get; }

        public event Action<PluginContainer> PermissionsChanged;

        public bool HasPermissions => (Permissions & NeedsPermissions) == NeedsPermissions;
        public bool HasDependencies { get; private set; } = true;
        public bool IsReady => HasPermissions && HasDependencies;
        public bool Loaded { get; internal set; }

        internal Dictionary<PluginPermissions, WeakReference<SecuredServiceBase>> SecuredServices { get; } = new Dictionary<PluginPermissions, WeakReference<SecuredServiceBase>>();
        internal List<IDisposable> DisposableServices { get; } = new List<IDisposable>();
        internal List<ScheduledDependencyInjection> ScheduledDependencyInjections { get; } = new List<ScheduledDependencyInjection>();
        internal Dictionary<EventContainer, Delegate> EventHandlers { get; } = new Dictionary<EventContainer, Delegate>();

        private Type pluginType;

        public PluginContainer(PluginInfo info, string source)
        {
            Info = info;
            Source = source;
            NeedsPermissions = PluginPermissions.None;
        }

        public PluginContainer(PluginBase plugin, PluginInfo info, Assembly assembly, AssemblyLoadContext loadContext, string source)
        {
            Plugin = plugin;
            Info = info;
            Assembly = assembly;
            LoadContext = loadContext;
            Source = source;

            NeedsPermissions = PluginPermissions.None;
            AssemblyName[] referencedAssemblies = Assembly.GetReferencedAssemblies();
            for (int i = 0; i < referencedAssemblies.Length; i++)
            {
                NeedsPermissions |= GetNeededAssemblyPermission(referencedAssemblies[i]);
            }

            pluginType = plugin.GetType();
            ClassName = pluginType.Name;
        }

        #region Services
        internal void RegisterSecuredService(SecuredServiceBase securedService)
        {
            if (!SecuredServices.ContainsKey(securedService.NeededPermission))
                SecuredServices.Add(securedService.NeededPermission, new WeakReference<SecuredServiceBase>(securedService));
        }

        internal void RegisterDisposableService(IDisposable disposableService)
        {
            DisposableServices.Add(disposableService);
        }
        #endregion

        #region Dependencies
        public void RegisterDependencies(PluginManager manager, ILogger? logger = null)
        {
            // FieldInfo[] and PropertyInfo[] can't be merged into MemberInfo[], since it includes methods etc. and MemberInfo doesn't have SetValue method

            FieldInfo[] fields = pluginType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var attribute = field.GetCustomAttribute<DependencyAttribute>();
                if (attribute != null)
                {
                    if (field.FieldType != typeof(PluginBase) && !field.FieldType.IsSubclassOf(typeof(PluginWrapper)))
                    {
                        logger?.LogWarning($"Failed injecting into {Info.Name}.{field.Name} property, because it's not PluginBase or PluginWrapper.");
                        continue;
                    }

                    string name = field.GetCustomAttribute<AliasAttribute>()?.Identifier ?? field.Name;
                    PluginBase dependency = GetDependency(manager, logger, name, attribute.GetMinVersion())?.Plugin;

                    if (dependency != null)
                    {
                        field.SetValue(Plugin, CreateInjection(field.FieldType, dependency, logger));
                    }
                    else
                    {
                        ScheduledDependencyInjections.Add(new ScheduledDependencyInjection(name,
                                                                                           attribute,
                                                                                           injection: plugin => field.SetValue(Plugin, CreateInjection(field.FieldType, plugin, logger))));
                        if (!attribute.Optional)
                            HasDependencies = false;
                    }
                }
            }

            PropertyInfo[] properties = pluginType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var attribute = property.GetCustomAttribute<DependencyAttribute>();
                if (attribute != null)
                {
                    if (property.PropertyType != typeof(PluginBase) && !property.PropertyType.IsSubclassOf(typeof(PluginWrapper)))
                    {
                        logger?.LogWarning($"Failed injecting into {Info.Name}.{property.Name} property, because it's not PluginBase or PluginWrapper.");
                        continue;
                    }

                    string name = property.GetCustomAttribute<AliasAttribute>()?.Identifier ?? property.Name;
                    PluginBase dependency = GetDependency(manager, logger, name, attribute.GetMinVersion())?.Plugin;

                    if (dependency != null)
                    {
                        property.SetValue(Plugin, CreateInjection(property.PropertyType, dependency, logger));
                    }
                    else
                    {
                        ScheduledDependencyInjections.Add(new ScheduledDependencyInjection(name,
                                                                                           attribute,
                                                                                           injection: plugin => property.SetValue(Plugin, CreateInjection(property.PropertyType, plugin, logger))));
                        if (!attribute.Optional)
                            HasDependencies = false;
                    }
                }
            }
        }

        private object CreateInjection(Type targetType, PluginBase plugin, ILogger? logger = null)
        {
            if (targetType == typeof(PluginBase))
            {
                return plugin;
            }
            else
            {
                object wrapper;
                try
                {
                    wrapper = Activator.CreateInstance(targetType);
                }
                catch
                {
                    logger?.LogWarning($"Failed while creating '{targetType.Name}', because it doesn't have accesible parameterless constructor.");
                    return null;
                }

                typeof(PluginWrapper).GetField("plugin", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wrapper, plugin);

                Type pluginType = plugin.GetType();
                var methods = pluginType.GetMethods();
                foreach (var property in targetType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(p => p.PropertyType.IsSubclassOf(typeof(Delegate))))
                {
                    string methodName = property.GetCustomAttribute<AliasAttribute>()?.Identifier ?? property.Name;
                    var delegateSignature = property.PropertyType.GetMethod("Invoke");
                    var returnType = delegateSignature.ReturnType;
                    var parameterTypes = delegateSignature.GetParameters().Select(p => p.ParameterType);
                    var method = methods.Where(m => m.Name == methodName).FirstOrDefault(m => m.ReturnType == returnType && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes));
                    if (method == null)
                    {
                        logger?.LogWarning($"Couldn't inject into {targetType.Name}.{property.Name}, because no method with matching name was found.");
                        continue;
                    }

                    Delegate @delegate;
                    try
                    {
                        @delegate = method.CreateDelegate(property.PropertyType, plugin);
                    }
                    catch
                    {
                        logger?.LogWarning($"Couldn't inject into {targetType.Name}.{property.Name}, because no method with matching signature was found.");
                        continue;
                    }
                    property.SetValue(wrapper, @delegate);
                }

                return wrapper;
            }
        }

        private PluginContainer GetDependency(PluginManager manager, ILogger logger, string name, Version minVersion)
        {
            foreach (var plugin in manager.Plugins)
            {
                if (IsValidDependency(name, minVersion, actual: plugin, logger))
                    return plugin;
            }

            return null;
        }

        public bool TryAddDependency(PluginContainer plugin, ILogger logger)
        {
            for (int i = 0; i < ScheduledDependencyInjections.Count; i++)
            {
                ScheduledDependencyInjection scheduledDependency = ScheduledDependencyInjections[i];
                if (IsValidDependency(scheduledDependency.Name, scheduledDependency.Info.GetMinVersion(), actual: plugin, logger))
                {
                    scheduledDependency.Inject(plugin.Plugin);
                    ScheduledDependencyInjections.RemoveAt(i--);
                }
            }

            if (!HasDependencies)
            {
                foreach (var scheduledDependency in ScheduledDependencyInjections)
                {
                    if (!scheduledDependency.Info.Optional)
                        return false;
                }
            }
            return HasDependencies = true;
        }

        private bool IsValidDependency(string expectedName, Version minVersion, PluginContainer actual, ILogger logger)
        {
            if (actual.Info.Name == expectedName || actual.ClassName == expectedName)
            {
                if (minVersion != null && actual.Info.Version != null && actual.Info.Version < minVersion)
                {
                    logger?.LogWarning($"Found matching dependency '{actual.Info.Name}', but with older version {actual.Info.Version} (minimum: {minVersion})");
                    return false;
                }
                return true;
            }

            return false;
        }
        #endregion

        #region Permissions
        public bool HasPermission(PluginPermissions permission)
        {
            if (permission == PluginPermissions.None)
                return true;

            return (permission & _permissions) != 0;
        }

        private void UpdatePermissions(PluginPermissions permissions)
        {
            _permissions = permissions;
            foreach (var (key, value) in SecuredServices)
            {
                if (value.TryGetTarget(out var service))
                {
                    service.HasPermission = HasPermission(key);
                }
                else
                {
                    SecuredServices.Remove(key);
                }
            }
            PermissionsChanged?.Invoke(this);
        }

        private PluginPermissions GetNeededAssemblyPermission(AssemblyName assembly)
        {
            if (assembly.Name.StartsWith("System.IO"))
                return PluginPermissions.FileAccess;

            if (assembly.Name.StartsWith("System.Net"))
                return PluginPermissions.InternetAccess;

            if (assembly.Name.StartsWith("System.Reflection"))
                return PluginPermissions.Reflection;

            if (assembly.Name.StartsWith("System.Runtime.Interop"))
                return PluginPermissions.Interop;

            if (assembly.Name.StartsWith("System.Diagnostics") && assembly.Name != "System.Diagnostics.Debug")
                return PluginPermissions.RunningSubprocesses;

            if (assembly.Name.StartsWith("Microsoft.CSharp"))
                return PluginPermissions.Compilation;

            if (!assembly.Name.StartsWith("System") && !assembly.Name.StartsWith("Microsoft") && !assembly.Name.StartsWith("Obsidian.API"))
                return PluginPermissions.ThirdPartyLibraries;

            return PluginPermissions.None;
        }
        #endregion
    }
}
