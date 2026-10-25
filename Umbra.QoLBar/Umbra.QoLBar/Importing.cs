using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Umbra.Common;

namespace Umbra.QoLBar;

/// <summary>
/// Serialization binder that maps QoLBar's short type names to the types of
/// this assembly, used for clipboard import/export.
/// </summary>
public class QoLSerializer : DefaultSerializationBinder
{
    private static readonly Type exportType = typeof(Importing.ExportInfo);
    private static readonly Type barType = typeof(BarCfg);
    private static readonly Type shortcutType = typeof(ShCfg);
    private static readonly Type conditionSetType = typeof(CndSetCfg);
    private static readonly Type conditionType = typeof(CndCfg);

    private const string exportShortName = "e";
    private const string barShortName = "b2";
    private const string shortcutShortName = "s2";
    private const string conditionSetShortName = "cs";
    private const string conditionShortName = "c";

    private static readonly System.Collections.Generic.Dictionary<string, Type> types = new()
    {
        [exportShortName] = exportType,
        [barShortName] = barType,
        [shortcutShortName] = shortcutType,
        [conditionSetShortName] = conditionSetType,
        [conditionShortName] = conditionType,
        ["b"] = barType,
        ["s"] = shortcutType,
    };

    public override Type BindToType(string assemblyName, string typeName)
    {
        if (typeName != null && types.TryGetValue(typeName, out var type)) return type;

        // Refuse to bind any other type to avoid deserialization gadgets.
        throw new JsonSerializationException($"Type '{typeName}' is not allowed.");
    }

    public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
    {
        if (types.ContainsValue(serializedType))
        {
            assemblyName = null;
            typeName = serializedType switch {
                _ when serializedType == exportType        => exportShortName,
                _ when serializedType == barType           => barShortName,
                _ when serializedType == shortcutType      => shortcutShortName,
                _ when serializedType == conditionSetType  => conditionSetShortName,
                _                                          => conditionShortName
            };
            return;
        }

        throw new JsonSerializationException($"Type '{serializedType.Name}' cannot be exported.");
    }
}

/// <summary>
/// Clipboard import/export of bars and condition sets. The wire format is
/// compatible with QoLBarCN exports.
/// </summary>
public static class Importing
{
    public class ExportInfo
    {
        public BarCfg b2;
        public ShCfg s2;
        public CndSetCfg cs;
        public string v = "1.0.0";
    }

    private static readonly QoLSerializer serializer = new();

    private static readonly JsonSerializerSettings settings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        SerializationBinder = serializer,
        DefaultValueHandling = DefaultValueHandling.Include,
    };

    private static string CompressString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        using var ms = new MemoryStream();
        using (var gs = new GZipStream(ms, CompressionMode.Compress))
            gs.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string DecompressString(string s)
    {
        var data = Convert.FromBase64String(s);
        using var ms = new MemoryStream(data);
        using var gs = new GZipStream(ms, CompressionMode.Decompress);
        using var r = new StreamReader(gs);
        return r.ReadToEnd();
    }

    private static string SerializeObject(object o) => JsonConvert.SerializeObject(o, Formatting.Indented, settings);

    private static T DeserializeObject<T>(string json) => JsonConvert.DeserializeObject<T>(json, settings);

    public static string ExportBar(BarCfg bar)
    {
        var info = new ExportInfo { b2 = bar };
        return CompressString(SerializeObject(info));
    }

    public static string ExportShortcut(ShCfg sh)
    {
        var info = new ExportInfo { s2 = sh };
        return CompressString(SerializeObject(info));
    }

    public static string ExportConditionSet(CndSetCfg set)
    {
        var info = new ExportInfo { cs = set };
        return CompressString(SerializeObject(info));
    }

    /// <summary>
    /// Imports a bar from a clipboard string. Accepts exports of this plugin
    /// as well as QoLBarCN bar exports.
    /// </summary>
    public static BarCfg ImportBar(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        try
        {
            var json = DecompressString(data.Trim());

            var info = DeserializeObject<ExportInfo>(json);
            if (info?.b2 != null) return info.b2;

            return DeserializeObject<BarCfg>(json);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to import bar: {e.Message}");
            return null;
        }
    }

    public static ShCfg ImportShortcut(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        try
        {
            var json = DecompressString(data.Trim());

            var info = DeserializeObject<ExportInfo>(json);
            if (info?.s2 != null) return info.s2;

            return DeserializeObject<ShCfg>(json);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to import shortcut: {e.Message}");
            return null;
        }
    }

    public static CndSetCfg ImportConditionSet(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        try
        {
            var json = DecompressString(data.Trim());
            var info = DeserializeObject<ExportInfo>(json);
            return info?.cs;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to import condition set: {e.Message}");
            return null;
        }
    }
}
