using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AncientChargedArms;

// Settings for the charge ammo this mod adds. Chances are matched by ammo class
// rather than by def name, so any caliber using that class is covered for free.
public class AncientChargedArmsSettings : ModSettings
{
    // Ceiling for each slider, as a fraction. Antigrain is kept much lower.
    private const float MaxSpawnChance = 100f;
    private const float MaxSpawnChanceAnti = 5f;

    // Notch size in percentage points.
    private const float NaniteStepPercent = 5f;
    private const float AntiStepPercent = 1f;

    // N.E and NANO share one slider.
    public static float NaniteChargeAmmoSpawnChance;

    public static float AntigrainChargeAmmoSpawnChance;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref NaniteChargeAmmoSpawnChance, "naniteChargeAmmoSpawnChance", 0f, true);
        Scribe_Values.Look(ref AntigrainChargeAmmoSpawnChance, "antigrainChargeAmmoSpawnChance", 0f, true);
        base.ExposeData();
    }

    public static void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();

        listing.Begin(inRect);

        if (!ResolveCe())
        {
            listing.Label("RequiresCombatExtended".Translate());
        }

        // Nanite charge ammo (N.E + NANO)
        NaniteChargeAmmoSpawnChance = SnapToStep(listing.SliderLabeled(
            LabelWithValue("NaniteChargeAmmoSpawnChance", NaniteChargeAmmoSpawnChance),
            NaniteChargeAmmoSpawnChance, 0f, MaxSpawnChance), NaniteStepPercent);
        SetAmmoClassChance("AW_NanoExplosive", NaniteChargeAmmoSpawnChance);
        SetAmmoClassChance("AW_FleshEatingNanites", NaniteChargeAmmoSpawnChance);

        // Antigrain ammo
        AntigrainChargeAmmoSpawnChance = SnapToStep(listing.SliderLabeled(
            LabelWithValue("AntigrainChargeAmmoSpawnChance", AntigrainChargeAmmoSpawnChance),
            AntigrainChargeAmmoSpawnChance, 0f, MaxSpawnChanceAnti), AntiStepPercent);
        SetAmmoClassChance("AW_Anti", AntigrainChargeAmmoSpawnChance);

        if (listing.ButtonText("RestoreDefaults".Translate()))
        {
            RestoreDefaults();
        }

        listing.End();
    }

    public static void RestoreDefaults()
    {
        NaniteChargeAmmoSpawnChance = 0f;
        AntigrainChargeAmmoSpawnChance = 0f;
    }

    private static string LabelWithValue(string translationKey, float chance)
    {
        return translationKey.Translate() + ": " + chance.ToString("0.#") + "%";
    }

    // Rounds a raw slider value onto a whole notch.
    private static float SnapToStep(float value, float stepPercent)
    {
        return Mathf.Round(value / stepPercent) * stepPercent;
    }

    // Grabbed by name so we don't have to build against CombatExtended.dll.
    private const string AmmoDefTypeName = "CombatExtended.AmmoDef";
    private const string AmmoClassFieldName = "ammoClass";

    private static FieldInfo _ammoClassField;
    private static IEnumerable _allAmmoDefs;
    private static bool _ceResolved;

    // Hits every ammo def in the class, whatever caliber it happens to be.
    private static void SetAmmoClassChance(string ammoClassName, float chancePercent)
    {
        if (!ResolveCe())
        {
            return;
        }

        foreach (object ammoDef in _allAmmoDefs)
        {
            if (_ammoClassField.GetValue(ammoDef) is Def ammoClass && ammoClass.defName == ammoClassName)
            {
                ((ThingDef)ammoDef).generateAllowChance = chancePercent / 100f;
            }
        }
    }

    // No CE loaded, nothing to touch.
    private static bool ResolveCe()
    {
        if (!_ceResolved)
        {
            _ceResolved = true;
            Type ammoDefType = AccessTools.TypeByName(AmmoDefTypeName);
            _ammoClassField = ammoDefType == null ? null : AccessTools.Field(ammoDefType, AmmoClassFieldName);
            _allAmmoDefs = ammoDefType == null ? null : AllDefsOfType(ammoDefType);
        }

        return _ammoClassField != null && _allAmmoDefs != null;
    }

    private static IEnumerable AllDefsOfType(Type defType)
    {
        PropertyInfo allDefs = typeof(DefDatabase<>)
            .MakeGenericType(defType)
            .GetProperty("AllDefs", BindingFlags.Public | BindingFlags.Static);

        return (IEnumerable)allDefs?.GetValue(null);
    }
}
