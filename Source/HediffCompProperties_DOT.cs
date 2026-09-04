using RimWorld;
using Verse;

namespace AncientChargedArms;

// A Hediff comp that deals damage over time, scaled by the Hediff's current severity.
// Optionally spawns a visual-only effect (e.g. an explosion fleck) on each damage tick.
public class HediffCompProperties_DOT : HediffCompProperties
{
    public HediffCompProperties_DOT()
    {
        compClass = typeof(HediffComp_DOT);
    }

    // Ticks between damage applications.
    public int intervalTicks = 120;

    // Damage dealt each tick = damagePerSeverity * parent.Severity.
    // When AoE is enabled (aoeDamageDef set), DoT is OFF by default unless this is > 0.
    public float damagePerSeverity = 1f;

    // Damage type applied. Falls back to Bite if not set in XML.
    public DamageDef damageDef;

    // Optional visual-only effect (a MoteDef, e.g. "Explosion"). No damage is dealt by it.
    public ThingDef moteDef = null;

    // Optional visual-only fleck (a FleckDef, e.g. "Spark"). No damage is dealt by it.
    public FleckDef fleckDef = null;

    // Scale passed to the visual effect.
    public float moteScale = 1f;

    // If set, spawns an AoE explosion around the pawn when it is downed.
    public DamageDef aoeDamageDef = null;

    // Base radius of the AoE explosion (in cells) at severity 1.
    public float aoeRadius = 3f;

    // Radius multiplier per unit of severity: finalRadius = aoeRadius * (1 + aoeRadiusPerSeverity * (severity - 1)).
    public float aoeRadiusPerSeverity = 0f;

    // Base damage dealt to each pawn caught in the AoE at severity 1.
    public float aoeDamage = 10f;

    // Damage multiplier per unit of severity: finalDamage = aoeDamage * (1 + aoeDamagePerSeverity * (severity - 1)).
    public float aoeDamagePerSeverity = 0f;

}
