using System;
using System.Collections;
using System.Collections.Generic;
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
            listing.End();
            return;
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
            // Re-apply the reset chances so the defs update immediately.
            SetAmmoClassChance("AW_NanoExplosive", NaniteChargeAmmoSpawnChance);
            SetAmmoClassChance("AW_FleshEatingNanites", NaniteChargeAmmoSpawnChance);
            SetAmmoClassChance("AW_Anti", AntigrainChargeAmmoSpawnChance);
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

    // Reflection handles, resolved once.
    private static FieldInfo _ammoClassField;
    private static bool _ceResolved;

    // Ammo defs grouped by class, built lazily and cached so the settings
    // window (which redraws every frame) never has to walk every ammo def.
    private static readonly Dictionary<string, List<ThingDef>> _ammoByClass = new();

    // Hits every ammo def in the class, whatever caliber it happens to be.
    private static void SetAmmoClassChance(string ammoClassName, float chancePercent)
    {
        if (!ResolveCe())
            return;

        if (!_ammoByClass.TryGetValue(ammoClassName, out List<ThingDef> list))
        {
            list = new List<ThingDef>();
            Type defType = _ammoClassField.DeclaringType;
            foreach (object ammoDef in AllDefsOfType(defType))
            {
                if (_ammoClassField.GetValue(ammoDef) is Def ammoClass && ammoClass.defName == ammoClassName)
                    list.Add((ThingDef)ammoDef);
            }
            _ammoByClass[ammoClassName] = list;
        }

        float chance = chancePercent / 100f;
        foreach (ThingDef def in list)
            def.generateAllowChance = chance;
    }

    // No CE loaded, nothing to touch.
    private static bool ResolveCe()
    {
        if (!_ceResolved)
        {
            _ceResolved = true;
            Type ammoDefType = AccessTools.TypeByName(AmmoDefTypeName);
            _ammoClassField = ammoDefType == null ? null : AccessTools.Field(ammoDefType, AmmoClassFieldName);
        }

        return _ammoClassField != null;
    }

    private static IEnumerable AllDefsOfType(Type defType)
    {
        PropertyInfo allDefs = typeof(DefDatabase<>)
            .MakeGenericType(defType)
            .GetProperty("AllDefs", BindingFlags.Public | BindingFlags.Static);

        return (IEnumerable)allDefs?.GetValue(null);
    }
}
