using System.Collections.Generic;
using GalaxyBudsClient.Platform.Model;

namespace GalaxyBudsClient.Platform.Interfaces;

public interface IHotkey
{
    List<ModifierKeys> Modifier { set; get; }
    List<Keys> Keys { set; get; }
}