using UnityEngine;
using Verse;

namespace AncientChargedArms;

/// <summary>
/// Main Mod class used to register the mod settings window.
/// </summary>
public class AncientChargedArms : Mod
{
    /// <summary>
    /// Mod settings reference.
    /// </summary>
    internal static AncientChargedArmsSettings settings;

    /// <summary>
    /// Constructor loads the mod settings.
    /// </summary>
    public AncientChargedArms(ModContentPack content) : base(content)
    {
        settings = GetSettings<AncientChargedArmsSettings>();
    }

    /// <summary>
    /// Draws the mod settings window.
    /// </summary>
    public override void DoSettingsWindowContents(Rect inRect)
    {
        AncientChargedArmsSettings.DoSettingsWindowContents(inRect);
        base.DoSettingsWindowContents(inRect);
    }

    /// <summary>
    /// Name of the settings category, shown in the mod settings list.
    /// </summary>
    public override string SettingsCategory()
    {
        return "AncientChargedArms".Translate();
    }
}
