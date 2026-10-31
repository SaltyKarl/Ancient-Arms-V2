using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AncientChargedArms;

/// <summary>
/// Applies the two switches from the mod settings.
/// Disabled weapons are pseudo-removed the way Cherry Picker does it: nothing is
/// deleted from the database, so a def still resolves for anything that points at
/// it, but every hook that could put it back into a game (map generation, trading,
/// crafting, quest rewards, raider loadouts) is cut. The new ammo is not removed at
/// all, it is patched into the stock Combat Extended ammo picked in the settings.
/// </summary>
[StaticConstructorOnStartup]
public static class AncientChargedArmsDefFilter
{
    private const string WeaponDefPrefix = "AW_";

    private const string AmmoDefTypeName = "CombatExtended.AmmoDef";
    private const string AmmoClassFieldName = "ammoClass";
    private const string AmmoSetDefTypeName = "CombatExtended.AmmoSetDef";
    private const string AmmoUserTypeName = "CombatExtended.CompProperties_AmmoUser";

    // The plain round of a stock charge set carries this ammo class.
    private const string StockAmmoClass = "Charged";

    // The ammo classes this mod adds on top of Combat Extended. Matched by class
    // rather than by def name so every caliber using the class is covered.
    private static readonly string[] ModAmmoClasses =
    {
        "AW_NanoExplosive",
        "AW_FleshEatingNanites",
        "AW_Anti",
    };

    // AmmoDef fields copied over from the stock round the mod ammo is patched into.
    private static readonly string[] CopiedAmmoFields =
    {
        "ammoClass",
        "detonateProjectile",
        "cookOffProjectile",
        "cookOffSound",
        "cookOffFlashScale",
        "cookOffSpeed",
        "defaultAmmoCount",
        "ammoCount",
        "isMortarAmmo",
        "spawnAsSiegeAmmo",
    };

    // Our ammo sets are named after the stock set of the same caliber plus this
    // suffix, so dropping the suffix lands on the stock Combat Extended set.
    private const string ModAmmoSetSuffix = "_ADACharged";
    private const string StockAmmoSetSuffix = "Charged";

    // The universal mechanoid cartridge used when the mech fallback is picked.
    private const string MechAmmoSetDefName = "AmmoSet_MechCharged";

    /// <summary>
    /// Combat Extended ammo def type, null when Combat Extended is not loaded.
    /// </summary>
    private static readonly Type AmmoDefType = AccessTools.TypeByName(AmmoDefTypeName);

    /// <summary>
    /// Combat Extended ammo class field, null when Combat Extended is not loaded.
    /// </summary>
    private static readonly FieldInfo AmmoClassField = AmmoDefType == null ? null : AccessTools.Field(AmmoDefType, AmmoClassFieldName);

    private static readonly FieldInfo AmmoLinkAmmoField = AccessTools.Field(AccessTools.TypeByName("CombatExtended.AmmoLink"), "ammo");

    private static readonly FieldInfo AmmoLinkProjectileField = AccessTools.Field(AccessTools.TypeByName("CombatExtended.AmmoLink"), "projectile");

    private static readonly MethodInfo RecipeDefRemove =
        AccessTools.Method(typeof(DefDatabase<RecipeDef>), "Remove", new[] { typeof(RecipeDef) });

    private static readonly FieldInfo AllRecipesCachedField = AccessTools.Field(typeof(ThingDef), "allRecipesCached");

    static AncientChargedArmsDefFilter()
    {
        try
        {
            // Captured before anything is patched: the spawn chance sliders key off
            // the ammo classes this mod adds, which patching replaces.
            CaptureModAmmoByClass();
            AncientChargedArmsSettings.ApplySpawnChances();

            if (AncientChargedArmsSettings.DisableWeapons)
                RetireWeapons();

            if (AncientChargedArmsSettings.DisableNewAmmo)
                PatchNewAmmo();
        }
        catch (Exception e)
        {
            Log.Error("[Ancient Charged Arms] Failed to apply the mod settings switches:\n" + e);
        }
    }

    /// <summary>
    /// True when Combat Extended is loaded and its ammo defs can be inspected.
    /// </summary>
    internal static bool CombatExtendedLoaded => AmmoClassField != null;

    /// <summary>
    /// The ammo defs this mod adds, grouped by the ammo class they had before any
    /// patching. The spawn chance sliders write to these defs.
    /// </summary>
    internal static readonly Dictionary<string, List<ThingDef>> ModAmmoByClass = new();

    private static void CaptureModAmmoByClass()
    {
        if (!CombatExtendedLoaded)
            return;

        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!IsModAmmo(def) || !(AmmoClassField.GetValue(def) is Def ammoClass))
                continue;

            if (!ModAmmoByClass.TryGetValue(ammoClass.defName, out List<ThingDef> list))
                ModAmmoByClass[ammoClass.defName] = list = new List<ThingDef>();

            list.Add(def);
        }
    }

    /// <summary>
    /// True for this mod's guns: an AW_ thing that carries a Combat Extended
    /// ammo user comp. Melee weapons have no ammo and are left alone.
    /// </summary>
    internal static bool IsModWeapon(ThingDef def)
    {
        if (def?.defName == null || !def.defName.StartsWith(WeaponDefPrefix, StringComparison.Ordinal))
            return false;

        return def.comps != null && def.comps.Any(comp => comp != null && comp.GetType().FullName == AmmoUserTypeName);
    }

    /// <summary>
    /// True for the ammo this mod adds (N.E, NANO, Antigrain).
    /// </summary>
    internal static bool IsModAmmo(ThingDef def)
    {
        if (def == null || !CombatExtendedLoaded || !AmmoDefType.IsInstanceOfType(def))
            return false;

        return AmmoClassField.GetValue(def) is Def ammoClass && ModAmmoClasses.Contains(ammoClass.defName);
    }

    /// <summary>
    /// Walks every def of the given def type through reflection, so this mod never
    /// has to build against CombatExtended.dll.
    /// </summary>
    internal static IEnumerable AllDefsOfType(Type defType)
    {
        PropertyInfo allDefs = typeof(DefDatabase<>)
            .MakeGenericType(defType)
            .GetProperty("AllDefs", BindingFlags.Public | BindingFlags.Static);

        return (IEnumerable)allDefs?.GetValue(null) ?? Array.Empty<Def>();
    }

    internal static Def FindDef(string defTypeName, string defName)
    {
        Type defType = AccessTools.TypeByName(defTypeName);
        if (defType == null)
            return null;

        foreach (object def in AllDefsOfType(defType))
        {
            if (def is Def typed && typed.defName == defName)
                return typed;
        }

        return null;
    }

    private static void RetireWeapons()
    {
        List<ThingDef> weapons = DefDatabase<ThingDef>.AllDefsListForReading.Where(IsModWeapon).ToList();
        foreach (ThingDef weapon in weapons)
            RetireThing(weapon);

        RemoveRecipesProducing(weapons);

        if (weapons.Count > 0)
            Log.Message("[Ancient Charged Arms] Mod weapons disabled, " + weapons.Count + " weapons removed from spawning, trading and crafting.");
    }

    private static void PatchNewAmmo()
    {
        if (!CombatExtendedLoaded)
            return;

        Type ammoSetDefType = AccessTools.TypeByName(AmmoSetDefTypeName);
        FieldInfo ammoTypesField = ammoSetDefType == null ? null : AccessTools.Field(ammoSetDefType, "ammoTypes");
        if (ammoTypesField == null || AmmoLinkAmmoField == null || AmmoLinkProjectileField == null)
        {
            Log.Warning("[Ancient Charged Arms] Combat Extended ammo set layout not recognised, the new ammo was left as is.");
            return;
        }

        // Snapshot first: patching rewrites the ammo class, so the mod ammo would
        // no longer be recognisable once the first set has been handled.
        HashSet<ThingDef> modAmmo = new(DefDatabase<ThingDef>.AllDefsListForReading.Where(IsModAmmo));
        HashSet<ThingDef> patchedDefs = new();
        int patched = 0;

        // Each caliber we ship has its own *_ADACharged ammo set, and that set is
        // what says which stock ammo its entries should turn into.
        foreach (object def in AllDefsOfType(ammoSetDefType))
        {
            if (!(def is Def modSet) || modSet.defName == null || !modSet.defName.EndsWith(ModAmmoSetSuffix, StringComparison.Ordinal))
                continue;

            string stockSetName = StockAmmoSetName(modSet.defName);
            Def stockSet = FindDef(AmmoSetDefTypeName, stockSetName);
            if (stockSet == null)
            {
                Log.Warning("[Ancient Charged Arms] " + stockSetName + " not found, " + modSet.defName + " was left as is.");
                continue;
            }

            ThingDef stockAmmo = FindStockAmmo(stockSet, ammoTypesField);
            ThingDef stockProjectile = stockAmmo == null ? null : FindStockProjectile(stockSet, stockAmmo, ammoTypesField);
            if (stockAmmo == null || stockProjectile == null)
            {
                Log.Warning("[Ancient Charged Arms] No stock ammo found in " + stockSetName + ", the new ammo was left as is.");
                continue;
            }

            foreach (object link in AmmoLinks(modSet, ammoTypesField))
            {
                if (!(AmmoLinkAmmoField.GetValue(link) is ThingDef ammo) || !modAmmo.Contains(ammo))
                    continue;

                if (patchedDefs.Add(ammo))
                {
                    PatchAmmo(ammo, stockAmmo);
                    patched++;
                }

                // The projectile a gun fires comes from the set, not from the ammo
                // def, so the link is what actually turns N.E into plain charged.
                AmmoLinkProjectileField.SetValue(link, stockProjectile);
            }
        }

        if (patched > 0)
        {
            string mode = AncientChargedArmsSettings.AmmoFallback == AmmoFallbackMode.MechCharged
                ? "mech charged ammo"
                : "same-caliber charge ammo";
            Log.Message("[Ancient Charged Arms] " + patched + " new ammo defs patched into " + mode + ".");
        }
    }

    // Makes one ammo def behave and weigh exactly like the stock round it is
    // patched into. Labels and graphics stay so the ammo stays recognisable.
    private static void PatchAmmo(ThingDef ammo, ThingDef stockAmmo)
    {
        foreach (string fieldName in CopiedAmmoFields)
        {
            FieldInfo source = AccessTools.Field(AmmoDefType, fieldName);
            if (source == null)
                continue;

            FieldInfo target = AccessTools.Field(ammo.GetType(), fieldName) ?? source;
            if (target.FieldType != source.FieldType)
                continue;

            target.SetValue(ammo, source.GetValue(stockAmmo));
        }

        ammo.statBases = CloneStatBases(stockAmmo.statBases);
        ammo.stackLimit = stockAmmo.stackLimit;
    }

    private static List<StatModifier> CloneStatBases(List<StatModifier> source)
    {
        if (source == null)
            return null;

        List<StatModifier> copy = new(source.Count);
        foreach (StatModifier modifier in source)
        {
            if (modifier?.stat == null)
                continue;

            copy.Add(new StatModifier { stat = modifier.stat, value = modifier.value });
        }

        return copy;
    }

    private static IEnumerable<object> AmmoLinks(Def ammoSet, FieldInfo ammoTypesField)
    {
        if (ammoTypesField.GetValue(ammoSet) is IEnumerable links)
        {
            foreach (object link in links)
            {
                if (link != null)
                    yield return link;
            }
        }
    }

    // The plain round of the stock set: the one in the regular Charged class.
    private static ThingDef FindStockAmmo(Def stockSet, FieldInfo ammoTypesField)
    {
        ThingDef firstStock = null;

        foreach (object link in AmmoLinks(stockSet, ammoTypesField))
        {
            if (!(AmmoLinkAmmoField.GetValue(link) is ThingDef ammo) || IsModAmmo(ammo))
                continue;

            if (firstStock == null)
                firstStock = ammo;

            if (AmmoClassField.GetValue(ammo) is Def ammoClass && ammoClass.defName == StockAmmoClass)
                return ammo;
        }

        return firstStock;
    }

    private static ThingDef FindStockProjectile(Def stockSet, ThingDef stockAmmo, FieldInfo ammoTypesField)
    {
        foreach (object link in AmmoLinks(stockSet, ammoTypesField))
        {
            if (AmmoLinkAmmoField.GetValue(link) == stockAmmo)
                return AmmoLinkProjectileField.GetValue(link) as ThingDef;
        }

        return null;
    }

    // Same field cuts Cherry Picker makes for a pseudo-removed ThingDef.
    private static void RetireThing(ThingDef def)
    {
        def.generateCommonality = 0f;
        def.generateAllowChance = 0f;
        def.tradeability = Tradeability.None;
        def.BaseMarketValue = 0f;
        def.thingCategories?.Clear();
        def.thingSetMakerTags?.Clear();
        def.tradeTags?.Clear();
        def.weaponTags?.Clear();
        def.weaponClasses?.Clear();
        def.recipeMaker?.recipeUsers?.Clear();
    }

    // Recipes are already generated by the time this runs, so they have to leave
    // the recipe database, otherwise the benches would still offer the bills.
    private static void RemoveRecipesProducing(List<ThingDef> things)
    {
        if (RecipeDefRemove == null || things.Count == 0)
            return;

        HashSet<ThingDef> retired = new(things);
        List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;

        for (int i = recipes.Count - 1; i >= 0; i--)
        {
            RecipeDef recipe = recipes[i];
            if (!Produces(recipe, retired))
                continue;

            recipe.recipeUsers?.Clear();
            RecipeDefRemove.Invoke(null, new object[] { recipe });
        }

        ClearRecipeCache();
    }

    // Benches build their recipe list lazily; drop it so it gets rebuilt without
    // the recipes that were just removed.
    private static void ClearRecipeCache()
    {
        if (AllRecipesCachedField == null)
            return;

        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (AllRecipesCachedField.GetValue(def) != null)
                AllRecipesCachedField.SetValue(def, null);
        }
    }

    private static bool Produces(RecipeDef recipe, HashSet<ThingDef> things)
    {
        if (recipe?.products == null)
            return false;

        foreach (ThingDefCountClass product in recipe.products)
        {
            if (product?.thingDef != null && things.Contains(product.thingDef))
                return true;
        }

        return false;
    }

    // Our ammo sets are the stock set name plus a suffix; dropping the suffix gives
    // the same-caliber stock set. The mech fallback ignores the caliber entirely.
    private static string StockAmmoSetName(string currentAmmoSet)
    {
        if (currentAmmoSet == null || !currentAmmoSet.EndsWith(ModAmmoSetSuffix, StringComparison.Ordinal))
            return null;

        if (AncientChargedArmsSettings.AmmoFallback == AmmoFallbackMode.MechCharged)
            return MechAmmoSetDefName;

        return currentAmmoSet.Substring(0, currentAmmoSet.Length - ModAmmoSetSuffix.Length) + StockAmmoSetSuffix;
    }
}
