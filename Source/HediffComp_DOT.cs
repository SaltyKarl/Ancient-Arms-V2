using RimWorld;
using Verse;

namespace AncientChargedArms;

public class HediffComp_DOT : HediffComp
{
    private int ticks;
    private bool wasDowned;

    private HediffCompProperties_DOT Props => (HediffCompProperties_DOT)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || !pawn.Spawned || pawn.Dead)
            return;

        if (pawn.Downed)
        {
            if (!wasDowned)
            {
                wasDowned = true;
                TriggerAoE(pawn);
            }
            return;
        }

        wasDowned = false;

        // When AoE is enabled, DoT is off by default unless explicitly opted in via damagePerSeverity > 0.
        bool dotEnabled = Props.aoeDamageDef == null || Props.damagePerSeverity > 0f;

        if (++ticks < Props.intervalTicks)
            return;

        ticks = 0;

        if (dotEnabled)
        {
            float amount = Props.damagePerSeverity * parent.Severity;
            if (amount > 0f)
                pawn.TakeDamage(new DamageInfo(Props.damageDef, amount, 1f));
        }

        if (Props.moteDef != null)
        {
            Mote mote = (Mote)ThingMaker.MakeThing(Props.moteDef);
            mote.exactPosition = pawn.Position.ToVector3Shifted();
            mote.Scale = Props.moteScale;
            mote.exactRotation = Rand.Range(0f, 360f);
            GenSpawn.Spawn(mote, pawn.Position, pawn.Map);
        }

        if (Props.fleckDef != null)
        {
            FleckMaker.Static(pawn.Position.ToVector3Shifted(), pawn.Map, Props.fleckDef, Props.moteScale);
        }
    }

    private void TriggerAoE(Pawn pawn)
    {
        if (pawn == null || !pawn.Spawned || Props.aoeDamageDef == null)
            return;

        float severity = parent.Severity;
        float radius = Props.aoeRadius * (1f + Props.aoeRadiusPerSeverity * (severity - 1f));
        float damage = Props.aoeDamage * (1f + Props.aoeDamagePerSeverity * (severity - 1f));

        // GenExplosion reads the DamageDef's built-in visuals (explosionCellFleck,
        // explosionColorCenter/Edge, soundExplosion, explosionHeatEnergyPerCell, etc.),
        // so no separate mote/fleck spawning is needed here.
        GenExplosion.DoExplosion(
            pawn.Position,
            pawn.Map,
            radius,
            Props.aoeDamageDef,
            null,
            damAmount: (int)damage,
            chanceToStartFire: 0f,
            damageFalloff: true);
    }

    public override void Notify_PawnDied(System.Nullable<DamageInfo> dinfo, Hediff culprit = null)
    {
        base.Notify_PawnDied(dinfo, culprit);
        TriggerAoE(Pawn);
    }
}
