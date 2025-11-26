using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enrichments;

public class EnrichmentElectroconductive : EnrichmentData
{
    public float boltRadius = 3f;
    public int minBolts = 3;
    public int maxBolts = 5;
    public float minImpactVelocity = 5f;

    public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemImbued(item, imbue, spellCastCharge);
        if (spellCastCharge is not SpellCastLightning) return;
        imbue.OnImbueHit -= OnImbueHit;
        imbue.OnImbueHit += OnImbueHit;
    }

    public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemUnimbued(item, imbue, spellCastCharge);
        if (spellCastCharge is not SpellCastLightning) return;
        imbue.OnImbueHit -= OnImbueHit;
    }

    private void OnImbueHit(SpellCastCharge spellData, float amount, bool fired, CollisionInstance hit, EventTime eventTime)
    {
        if (spellData is SpellCastLightning spellCastLightning && hit.targetColliderGroup?.collisionHandler?.Entity is Creature creature && !creature.isPlayer && hit.impactVelocity.sqrMagnitude > minImpactVelocity * minImpactVelocity)
            creature.StartCoroutine(BoltRoutine(hit.contactPoint, spellCastLightning, creature));
    }

    IEnumerator BoltRoutine(Vector3 startPos, SpellCastLightning spellCastLightning, Creature startCreature)
    {
        int boltCount = Random.Range(minBolts, maxBolts);
        Creature currentCreature = startCreature;
        Vector3 currentPos = startPos;

        HashSet<Creature> hitCreatures = new() { currentCreature };

        for (int i = 0; i < boltCount; i++)
        {
            Creature nextCreature = null;
            float closestDist = float.MaxValue;

            foreach (Creature creature in Creature.InRadius(currentPos, boltRadius))
            {
                if (hitCreatures.Contains(creature) || creature.isPlayer) continue;

                float dist = Vector3.Distance(currentPos, creature.ragdoll.targetPart.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nextCreature = creature;
                }
            }

            if (nextCreature == null) yield break;

            spellCastLightning.PlayBolt(currentCreature.ragdoll.targetPart.transform, nextCreature.ragdoll.targetPart.transform);
            spellCastLightning.boltHitEffectData.Spawn(nextCreature.ragdoll.targetPart.transform);
            nextCreature.TryPush(Creature.PushType.Magic, (nextCreature.ragdoll.targetPart.transform.position - currentPos).normalized, 1);
            if (Random.Range(0, 2) == 1) nextCreature.Inflict("Electrocute", this, Random.Range(3f, 6f));

            hitCreatures.Add(nextCreature);
            currentCreature = nextCreature;
            currentPos = nextCreature.ragdoll.targetPart.transform.position;

            yield return Yielders.ForSeconds(0.085f);
        }
    }
}