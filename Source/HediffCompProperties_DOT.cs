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

    // Damage dealt each tick at severity 1.
    // When AoE is enabled (aoeDamageDef set), DoT is OFF by default unless this is > 0.
    public float damagePerSeverity = 1f;

    // Exponent of the DoT power curve: damagePerTick = damagePerSeverity * severity^damagePerSeverityExponent.
    // 1 = linear, < 1 = diminishing returns, 0 = flat damage.
    public float damagePerSeverityExponent = 1f;

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

    // Exponent of the AoE radius power curve: finalRadius = aoeRadius * severity^aoeRadiusExponent.
    // 1 = linear, < 1 = diminishing returns, 0 = flat radius.
    public float aoeRadiusExponent = 1f;

    // Base damage dealt to each pawn caught in the AoE at severity 1.
    public float aoeDamage = 10f;

    // Exponent of the AoE damage power curve: finalDamage = aoeDamage * severity^aoeDamageExponent.
    // 1 = linear, < 1 = diminishing returns, 0 = flat damage.
    public float aoeDamageExponent = 1f;

}
