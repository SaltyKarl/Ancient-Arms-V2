using UnityEngine;
using RimWorld;
using Verse;

namespace AncientChargedArms;

public partial class HediffComp_DOT : HediffComp
{
    private int ticks;
    private bool wasTriggered;
    private HediffCompProperties_DOT _props;

    // Cache the cast of the base `props` so we don't re-cast every tick.
    private HediffCompProperties_DOT Props => _props ??= (HediffCompProperties_DOT)props;

    public override void CompPostTick(ref float severityAdjustment)
    {
        base.CompPostTick(ref severityAdjustment);

        Pawn pawn = Pawn;
        if (pawn == null || !pawn.Spawned)
            return;

        // Trigger the AoE once when the pawn goes down or dies, whichever happens first.
        if (pawn.Downed || pawn.Dead)
        {
            if (!wasTriggered)
            {
                wasTriggered = true;
                TriggerAoE(pawn);
            }
            return;
        }

        wasTriggered = false;

        // When AoE is enabled, DoT is off by default unless explicitly opted in via damagePerSeverity > 0.
        bool dotEnabled = Props.aoeDamageDef == null || Props.damagePerSeverity > 0f;

        if (++ticks < Props.intervalTicks)
            return;

        ticks = 0;

        if (dotEnabled)
        {
            float severity = parent.Severity;
            float amount = Props.damagePerSeverityExponent == 1f
                ? Props.damagePerSeverity * severity
                : Props.damagePerSeverity * Mathf.Pow(severity, Props.damagePerSeverityExponent);
            if (amount > 0f)
            {
                DamageDef def = Props.damageDef ?? DamageDefOf.Bite;
                pawn.TakeDamage(new DamageInfo(def, amount, 1f));
            }
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

        float severity = Mathf.Max(parent.Severity, 1f);
        float radius = Props.aoeRadiusExponent == 1f
            ? Props.aoeRadius * severity
            : Props.aoeRadius * Mathf.Pow(severity, Props.aoeRadiusExponent);
        float damage = Props.aoeDamageExponent == 1f
            ? Props.aoeDamage * severity
            : Props.aoeDamage * Mathf.Pow(severity, Props.aoeDamageExponent);

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
            damageFalloff: true
        );

        // The HeDiff this comp belongs to is `parent`; remove it so the AoE doesn't re-trigger.
        pawn.health.RemoveHediff(parent);
    }


}
