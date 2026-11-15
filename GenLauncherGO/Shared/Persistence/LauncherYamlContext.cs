using GenLauncherGO.Features.Mods;
using GenLauncherGO.Features.Settings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;

namespace GenLauncherGO.Shared.Persistence;

/// <summary>
///     Compile-time YAML metadata for every document the launcher reads or writes, so serialization needs no
///     reflection under Native AOT. A document type missing here fails at run time, not at build time.
/// </summary>
/// <remarks>
///     Public because the generator declares its half of this partial class public. The generator binds internal
///     properties, so registered types mark those <see cref="YamlIgnoreAttribute" />, and it cannot assign init-only or
///     private setters. It would also write get-only computed properties that it then cannot read back, so both
///     builders see only properties with a setter; that also skips the computed keys earlier versions wrote.
/// </remarks>
[YamlStaticContext]
[YamlSerializable(typeof(LauncherPreferencesSchemaDocument))]
[YamlSerializable(typeof(LauncherPreferencesDocument))]
[YamlSerializable(typeof(LegacyLauncherPreferencesDocument))]
[YamlSerializable(typeof(LauncherInstallationsDocument))]
[YamlSerializable(typeof(LauncherSharedPreferencesDocument))]
[YamlSerializable(typeof(LauncherGamePreferencesSetDocument))]
[YamlSerializable(typeof(LauncherGamePreferencesDocument))]
[YamlSerializable(typeof(LauncherCustomExecutableDocument))]
[YamlSerializable(typeof(LauncherContentState))]
[YamlSerializable(typeof(LauncherContentEntryState))]
[YamlSerializable(typeof(LauncherContentVersionState))]
[YamlSerializable(typeof(LauncherContentTheme))]
[YamlSerializable(typeof(LegacyLauncherCatalogDocument))]
[YamlSerializable(typeof(LegacyCatalogAdvertisingReference))]
[YamlSerializable(typeof(LegacyCatalogModificationReference))]
[YamlSerializable(typeof(LegacyContentManifest))]
public sealed partial class LauncherYamlContext : StaticContext
{
    /// <summary>
    ///     Gets the shared deserializer, which ignores unknown keys: the backend may add fields, and settings files
    ///     written by other launcher versions may carry retired ones.
    /// </summary>
    public static IDeserializer Deserializer { get; } = new StaticDeserializerBuilder(new LauncherYamlContext())
        .WithTypeInspector(inner => new ReadableAndWritablePropertiesTypeInspector(inner))
        .IgnoreUnmatchedProperties()
        .Build();

    public static ISerializer Serializer { get; } = new StaticSerializerBuilder(new LauncherYamlContext())
        .WithTypeInspector(inner => new ReadableAndWritablePropertiesTypeInspector(inner))
        .Build();
}
