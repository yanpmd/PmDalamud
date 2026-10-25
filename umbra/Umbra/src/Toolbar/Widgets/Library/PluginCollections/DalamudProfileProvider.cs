using System.Collections;
using System.Reflection;
using Dalamud.Plugin;

namespace Umbra.Widgets.Library.PluginCollections;

internal readonly record struct DalamudProfile(string Name, bool IsEnabled, bool IsDefault);

/// <summary>
/// Reads the plugin collections ("profiles") known to Dalamud.
///
/// Dalamud only exposes these through internal types
/// (Dalamud.Plugin.Internal.Profiles), so this works on reflection. Nothing
/// here is allowed to throw: whenever the internal layout differs from what we
/// expect, <see cref="GetProfiles"/> returns an empty list instead of breaking
/// the widget.
/// </summary>
internal static class DalamudProfileProvider
{
    private const BindingFlags AllInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _resolved;

    private static Type?          _managerType;
    private static object?        _manager;
    private static object?        _defaultProfile;
    private static PropertyInfo?  _profilesProperty;
    private static PropertyInfo?  _nameProperty;
    private static PropertyInfo?  _enabledProperty;
    private static PropertyInfo?  _defaultProperty;

    /// <summary>Whether plugin collections can be read at all.</summary>
    public static bool IsSupported => HasLiveSource || DalamudProfileConfigReader.IsAvailable;

    private static bool HasLiveSource
    {
        get {
            Resolve();

            return _manager is not null && _profilesProperty is not null;
        }
    }

    public static IReadOnlyList<DalamudProfile> GetProfiles()
    {
        IReadOnlyList<DalamudProfile> profiles = GetLiveProfiles();

        // Fall back to what Dalamud persisted in its own config file.
        return profiles.Count > 0 ? profiles : DalamudProfileConfigReader.Read();
    }

    private static IReadOnlyList<DalamudProfile> GetLiveProfiles()
    {
        if (!HasLiveSource) return [];

        try {
            if (_profilesProperty!.GetValue(_manager) is not IEnumerable profiles) return [];

            List<DalamudProfile> result = [];

            foreach (object? profile in profiles) {
                if (profile is null) continue;

                // The default profile is Dalamud's built-in fallback. It is not
                // a collection the user can pick, so keep it out of the list.
                if (_defaultProfile is not null && ReferenceEquals(profile, _defaultProfile)) continue;
                if (_defaultProperty?.GetValue(profile) is true) continue;

                string name = _nameProperty?.GetValue(profile) as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;

                result.Add(
                    new(
                        name,
                        _enabledProperty?.GetValue(profile) is true,
                        _defaultProperty?.GetValue(profile) is true
                    )
                );
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

            return result;
        } catch (Exception e) {
            Logger.Warning($"Failed to read Dalamud plugin collections: {e.Message}");

            return [];
        }
    }

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        try {
            Assembly dalamud = typeof(IDalamudPluginInterface).Assembly;

            _managerType = FindManagerType(dalamud);

            if (_managerType is null) {
                Logger.Warning("Unable to find Dalamud's plugin collection manager type.");

                return;
            }

            _profilesProperty = FindProfilesProperty(_managerType);

            if (_profilesProperty is null) {
                Logger.Warning($"Unable to find the collection list on {_managerType.FullName}.");

                return;
            }

            Type? profileType = GetProfileType(_profilesProperty, dalamud);

            if (profileType is not null) {
                _nameProperty    = FindMemberProperty(profileType, typeof(string), "Name", "Title", "DisplayName");
                _enabledProperty = FindMemberProperty(profileType, typeof(bool), "IsEnabled", "Enabled", "IsActive");
                _defaultProperty = FindMemberProperty(profileType, typeof(bool), "IsDefaultProfile", "IsDefault");

                Logger.Debug(
                    $"Resolved plugin collections: {_managerType.FullName} / {profileType.FullName}, "
                    + $"name={_nameProperty?.Name ?? "?"}, enabled={_enabledProperty?.Name ?? "?"}"
                );
            }

            _manager = ResolveManagerInstance(_managerType);

            if (_manager is null) {
                Logger.Warning($"Unable to locate an instance of {_managerType.FullName}.");
            } else {
                _defaultProfile = _managerType.GetProperty("DefaultProfile", AllInstance)?.GetValue(_manager);
            }
        } catch (Exception e) {
            Logger.Warning($"Unable to initialise the plugin collection reader: {e.Message}");

            _manager = null;
        }
    }

    private static Type? FindManagerType(Assembly dalamud)
    {
        Type[] types = GetLoadableTypes(dalamud);

        return types.FirstOrDefault(
                   type => type is { IsClass: true, IsAbstract: false }
                           && type.Name is "ProfileManager" or "CollectionManager"
               )
               ?? types.FirstOrDefault(
                   type => type is { IsClass: true, IsAbstract: false }
                           && (type.Name.EndsWith("ProfileManager") || type.Name.EndsWith("CollectionManager"))
               );
    }

    private static PropertyInfo? FindProfilesProperty(Type managerType)
    {
        PropertyInfo[] properties = managerType.GetProperties(AllInstance);

        return properties.FirstOrDefault(
                   property => property.Name is "Profiles" or "Collections"
                               && typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                               && property.PropertyType != typeof(string)
               )
               ?? properties.FirstOrDefault(
                   property => typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                               && property.PropertyType != typeof(string)
               );
    }

    private static Type? GetProfileType(PropertyInfo profilesProperty, Assembly dalamud)
    {
        Type propertyType = profilesProperty.PropertyType;

        if (propertyType.IsGenericType && propertyType.GetGenericArguments() is [Type elementType]) {
            return elementType;
        }

        return GetLoadableTypes(dalamud).FirstOrDefault(
            type => type is { IsClass: true, IsAbstract: false, Namespace: not null }
                    && type.Namespace.Contains("Profiles", StringComparison.OrdinalIgnoreCase)
                    && type.Name is "Profile" or "Collection"
        );
    }

    private static PropertyInfo? FindMemberProperty(Type type, Type propertyType, params string[] names)
    {
        PropertyInfo[] properties = type.GetProperties(AllInstance);

        foreach (string name in names) {
            PropertyInfo? match = properties.FirstOrDefault(
                property => property.Name == name && property.PropertyType == propertyType
            );

            if (match is not null) return match;
        }

        return null;
    }

    private static object? ResolveManagerInstance(Type managerType)
    {
        // 1) Dalamud hands out its own services through the plugin interface.
        if (TryGetService(Framework.DalamudPlugin, managerType) is { } service) return service;

        // 2) Some Dalamud services expose a singleton instance.
        if (TryGetStaticInstance(managerType) is { } singleton) return singleton;

        // 2) ... or through a service scope held by it.
        foreach (FieldInfo field in Framework.DalamudPlugin.GetType().GetFields(AllInstance)) {
            if (!typeof(IServiceProvider).IsAssignableFrom(field.FieldType)) continue;

            object? provider;

            try {
                provider = field.GetValue(Framework.DalamudPlugin);
            } catch {
                continue;
            }

            if (provider is null) continue;
            if (TryGetService(provider, managerType) is { } nested) return nested;
        }

        // 3) Last resort: walk the object graph reachable from the plugin interface.
        return TryFindInstance(managerType);
    }

    private static object? TryGetStaticInstance(Type managerType)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        try {
            foreach (PropertyInfo property in managerType.GetProperties(flags)) {
                if (!managerType.IsAssignableFrom(property.PropertyType)) continue;

                if (property.GetValue(null) is { } instance) return instance;
            }

            foreach (FieldInfo field in managerType.GetFields(flags)) {
                if (!managerType.IsAssignableFrom(field.FieldType)) continue;

                if (field.GetValue(null) is { } instance) return instance;
            }
        } catch {
            // Ignore, this is only a fallback.
        }

        return null;
    }

    private static object? TryGetService(object provider, Type serviceType)
    {
        try {
            object? service = provider is IServiceProvider serviceProvider
                ? serviceProvider.GetService(serviceType)
                : null;

            return service is not null && serviceType.IsInstanceOfType(service) ? service : null;
        } catch {
            return null;
        }
    }

    private static object? TryFindInstance(Type managerType)
    {
        try {
            Queue<(object Instance, int Depth)> queue   = new();
            HashSet<object>                     visited = new(ReferenceEqualityComparer.Instance);

            queue.Enqueue((Framework.DalamudPlugin, 0));

            var budget = 5000;

            while (queue.Count > 0 && budget-- > 0) {
                (object instance, int depth) = queue.Dequeue();

                if (!visited.Add(instance)) continue;

                const BindingFlags flags = AllInstance | BindingFlags.DeclaredOnly;

                for (Type? type = instance.GetType(); type is not null && type != typeof(object); type = type.BaseType) {
                    foreach (FieldInfo field in type.GetFields(flags)) {
                        if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string)) continue;

                        object? value;

                        try {
                            value = field.GetValue(instance);
                        } catch {
                            continue;
                        }

                        if (value is null) continue;

                        if (managerType.IsInstanceOfType(value)) return value;

                        if (depth >= 4) continue;

                        if (value is IEnumerable enumerable and not string) {
                            var count = 0;

                            foreach (object? item in enumerable) {
                                if (item is null || count++ > 256) break;

                                if (managerType.IsInstanceOfType(item)) return item;

                                queue.Enqueue((item, depth + 1));
                            }

                            continue;
                        }

                        queue.Enqueue((value, depth + 1));
                    }
                }
            }
        } catch {
            // Reflection is best-effort only.
        }

        return null;
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try {
            return assembly.GetTypes();
        } catch (ReflectionTypeLoadException e) {
            return e.Types.Where(type => type is not null).ToArray()!;
        } catch {
            return [];
        }
    }
}
