using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using GalaxyBudsClient.Model;
using GalaxyBudsClient.Platform.Model;

namespace GalaxyBudsClient.Model.Config;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    AllowTrailingCommas = true, 
    IgnoreReadOnlyProperties = true,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SettingsData))]
[JsonSerializable(typeof(CustomAction))]
[JsonSerializable(typeof(Hotkey))]
[JsonSerializable(typeof(Device))]
[JsonSerializable(typeof(ObservableCollection<Hotkey>))]
[JsonSerializable(typeof(ObservableCollection<Device>))]
[JsonSerializable(typeof(List<ModifierKeys>))]
[JsonSerializable(typeof(List<Keys>))]
public partial class SettingsSerializerContext : JsonSerializerContext;