using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments;

/// <summary>
/// Written by Phantom
/// </summary>
public class EnrichmentMassDriver : EnrichmentData
{
    public string effectId;
    public float searchRadius = 3f;
    public float pointOffset = 1.5f;
    public float forceMultiplier = 15f;
    public float requiredVelocity = 5f;
    public float creatureCooldown = 2f;
    
    public EffectData effectData;

    private List<Creature> seenCreatures = new();
    
    public bool IsValidTarget(Creature target, SpellCastCharge spellData) => target != null && !target.isCulled && target != spellData?.spellCaster.ragdollHand.creature && !seenCreatures.Contains(target);

    public override void OnCatalogRefresh()
    {
        base.OnCatalogRefresh();
        effectData = Catalog.GetData<EffectData>(effectId);
    }

    public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemImbued(item, imbue, spellCastCharge);
        if (spellCastCharge is not SpellCastGravity) return;
        imbue.OnImbueHit -= OnImbueHit;
        imbue.OnImbueHit += OnImbueHit;
    }

    public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemUnimbued(item, imbue, spellCastCharge);
        imbue.OnImbueHit -= OnImbueHit;
    }

    private void OnImbueHit(SpellCastCharge spellData, float amount, bool fired, CollisionInstance hit, EventTime eventTime)
    {
        Creature hitCreature = hit?.targetColliderGroup?.collisionHandler?.ragdollPart?.ragdoll?.creature;
        if (hit == null || hitCreature is null || spellData is not SpellCastGravity || fired || eventTime != EventTime.OnStart || !IsValidTarget(hitCreature, spellData) || hit.impactVelocity.sqrMagnitude < requiredVelocity * requiredVelocity) return;
        
        foreach (Creature creature in Creature.InRadius(hitCreature.ragdoll.targetPart.transform.position, searchRadius, creature => IsValidTarget(creature, spellData) && creature != hitCreature))
        {
            Vector3 start = creature.ragdoll.targetPart.transform.position - hit.impactVelocity.normalized * pointOffset;
            Vector3 direction = hit.impactVelocity.normalized;
            creature.TryPush(Creature.PushType.Magic, direction, 1, RagdollPart.Type.Torso);
            creature.AddForce(direction * forceMultiplier, ForceMode.Impulse);
            effectData?.Spawn(start, Quaternion.LookRotation(direction)).Play();
            seenCreatures.Add(creature);
            creature.StartCoroutine(CooldownRoutine(creature));
        }
    }
    
    private IEnumerator CooldownRoutine(Creature target)
    {
        yield return Yielders.ForRealSeconds(creatureCooldown);
        seenCreatures.Remove(target);
    }
}