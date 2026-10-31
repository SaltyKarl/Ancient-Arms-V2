using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AncientChargedArms;

// What the guns fall back to once the mod's own ammo is switched off.
public enum AmmoFallbackMode
{
    // Stock Combat Extended charge ammo of the same caliber, e.g. AmmoSet_6x24mmCharged.
    SameCaliberCharge,

    // The universal mechanoid cartridge, AmmoSet_MechCharged.
    MechCharged,
}

// Settings for the charge ammo this mod adds. Chances are matched by ammo class
// rather than by def name, so any caliber using that class is covered for free.
public class AncientChargedArmsSettings : ModSettings
{
    // Ceiling for each slider, as a fraction. Antigrain is kept much lower.
    private const float MaxSpawnChance = 25f;
    private const float MaxSpawnChanceAnti = 5f;

    // Notch size in percentage points.
    private const float NaniteStepPercent = 5f;
    private const float AntiStepPercent = 1f;

    // N.E and NANO share one slider.
    public static float NaniteChargeAmmoSpawnChance;

    public static float AntigrainChargeAmmoSpawnChance;

    // The two switches. Both are read once at startup, so flipping them needs a
    // game restart before they do anything.
    public static bool DisableWeapons;

    public static bool DisableNewAmmo;

    // What the guns are patched to while the mod's ammo is switched off.
    public static AmmoFallbackMode AmmoFallback = AmmoFallbackMode.SameCaliberCharge;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref NaniteChargeAmmoSpawnChance, "naniteChargeAmmoSpawnChance", 0f, true);
        Scribe_Values.Look(ref AntigrainChargeAmmoSpawnChance, "antigrainChargeAmmoSpawnChance", 0f, true);
        Scribe_Values.Look(ref DisableWeapons, "disableWeapons", false);
        Scribe_Values.Look(ref DisableNewAmmo, "disableNewAmmo", false);
        Scribe_Values.Look(ref AmmoFallback, "ammoFallback", AmmoFallbackMode.SameCaliberCharge);
        base.ExposeData();
    }

    public static void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();

        listing.Begin(inRect);

        listing.CheckboxLabeled("DisableWeapons".Translate(), ref DisableWeapons, "DisableWeaponsDesc".Translate());
        listing.CheckboxLabeled("DisableNewAmmo".Translate(), ref DisableNewAmmo, "DisableNewAmmoDesc".Translate());

        if (!AncientChargedArmsDefFilter.CombatExtendedLoaded)
        {
            listing.Label("RequiresCombatExtended".Translate());
            listing.End();
            return;
        }

        listing.Gap();

        if (DisableNewAmmo)
        {
            // What the mod's own rounds are patched into.
            listing.Label("AmmoFallbackHeader".Translate());

            if (listing.RadioButton("AmmoFallbackSameCaliber".Translate(), AmmoFallback == AmmoFallbackMode.SameCaliberCharge,
                    0f, "AmmoFallbackSameCaliberDesc".Translate()))
                AmmoFallback = AmmoFallbackMode.SameCaliberCharge;

            if (listing.RadioButton("AmmoFallbackMech".Translate(), AmmoFallback == AmmoFallbackMode.MechCharged,
                    0f, "AmmoFallbackMechDesc".Translate()))
                AmmoFallback = AmmoFallbackMode.MechCharged;

            listing.Gap();
        }

        // Nanite charge ammo (N.E + NANO)
        NaniteChargeAmmoSpawnChance = SnapToStep(listing.SliderLabeled(
            LabelWithValue("NaniteChargeAmmoSpawnChance", NaniteChargeAmmoSpawnChance),
            NaniteChargeAmmoSpawnChance, 0f, MaxSpawnChance), NaniteStepPercent);

        // Antigrain ammo
        AntigrainChargeAmmoSpawnChance = SnapToStep(listing.SliderLabeled(
            LabelWithValue("AntigrainChargeAmmoSpawnChance", AntigrainChargeAmmoSpawnChance),
            AntigrainChargeAmmoSpawnChance, 0f, MaxSpawnChanceAnti), AntiStepPercent);

        ApplySpawnChances();

        if (listing.ButtonText("RestoreDefaults".Translate()))
        {
            RestoreDefaults();
            // Re-apply the reset chances so the defs update immediately.
            ApplySpawnChances();
        }

        listing.Label("SwitchesNeedRestart".Translate());

        listing.End();
    }

    public static void RestoreDefaults()
    {
        NaniteChargeAmmoSpawnChance = 0f;
        AntigrainChargeAmmoSpawnChance = 0f;
        DisableWeapons = false;
        DisableNewAmmo = false;
        AmmoFallback = AmmoFallbackMode.SameCaliberCharge;
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

    // Pushes both sliders onto the ammo defs. Works the same whether the ammo is
    // still the mod's own rounds or has been patched into stock rounds, because the
    // def list is captured before any patching happens.
    public static void ApplySpawnChances()
    {
        SetAmmoClassChance("AW_NanoExplosive", NaniteChargeAmmoSpawnChance);
        SetAmmoClassChance("AW_FleshEatingNanites", NaniteChargeAmmoSpawnChance);
        SetAmmoClassChance("AW_Anti", AntigrainChargeAmmoSpawnChance);
    }

    // Hits every ammo def that used the class, whatever caliber it happens to be.
    private static void SetAmmoClassChance(string ammoClassName, float chancePercent)
    {
        if (!AncientChargedArmsDefFilter.ModAmmoByClass.TryGetValue(ammoClassName, out List<ThingDef> list))
            return;

        float chance = chancePercent / 100f;
        foreach (ThingDef def in list)
            def.generateAllowChance = chance;
    }
}
