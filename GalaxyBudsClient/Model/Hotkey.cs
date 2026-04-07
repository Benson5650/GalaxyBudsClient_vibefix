using System.Collections.Generic;
using GalaxyBudsClient.Model.Constants;
using GalaxyBudsClient.Platform.Interfaces;
using GalaxyBudsClient.Platform.Model;
using GalaxyBudsClient.Utils.Extensions;
using ReactiveUI;

namespace GalaxyBudsClient.Model;

/*
 * NOTE: Do NOT use [Reactive] from ReactiveUI.SourceGenerators here.
 * The JSON SourceGenerator (System.Text.Json AOT) cannot see properties generated
 * by other source generators (source generator chaining is unsupported).
 * Use ReactiveUI.Fody.Helpers.Reactive instead, which uses IL weaving at compile
 * time and is therefore visible to all subsequent tooling.
 * See also: SettingsData.cs for the same workaround.
 */
public class Hotkey : ReactiveObject, IHotkey
{
    [ReactiveUI.Fody.Helpers.Reactive] public List<ModifierKeys> Modifier { get; set; } = [];
    [ReactiveUI.Fody.Helpers.Reactive] public List<Keys> Keys { get; set; } = [];
    [ReactiveUI.Fody.Helpers.Reactive] public Event Action { get; set; }

    internal string ActionName => Action.GetLocalizedDescription();
    internal string HotkeyName => Keys.AsHotkeyString(Modifier);

    public override string ToString()
    {
        return Keys.AsHotkeyString(Modifier) + ": " + Action.GetLocalizedDescription();
    }

    public static Hotkey Empty => new();
}
