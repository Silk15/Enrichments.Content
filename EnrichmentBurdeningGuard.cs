using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments;

public class EnrichmentBurdeningGuard : EnrichmentData
{
    public string effectId;
    public float forceMultiplier = 15f;
    public float requiredVelocity = 0.5f;
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
        EventManager.onCreatureAttackParry -= OnCreatureAttackParry;
        EventManager.onCreatureAttackParry += OnCreatureAttackParry;
    }
    
    public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemUnimbued(item, imbue, spellCastCharge);
        EventManager.onCreatureAttackParry -= OnCreatureAttackParry;
    }

    private void OnCreatureAttackParry(Creature parriedcreature, Item parrieditem, Creature parryingcreature, Item parryingitem, CollisionInstance hit)
    {
        bool hasImbue = parryingitem.imbues.Find(x => x.spellCastBase is SpellCastGravity) != null;
        if (!hasImbue || !EnrichmentManager.HasEnrichment(parryingitem, id) || !IsValidTarget(parriedcreature, null) || hit.impactVelocity.sqrMagnitude < requiredVelocity * requiredVelocity) return;
        
        Vector3 direction = parriedcreature.ragdoll.targetPart.transform.position - hit.contactPoint;
        parriedcreature.TryPush(Creature.PushType.Magic, direction, 1, RagdollPart.Type.Torso);
        parriedcreature.AddForce(direction * forceMultiplier, ForceMode.Impulse);
        effectData?.Spawn(hit.contactPoint, Quaternion.LookRotation(direction)).Play();
        seenCreatures.Add(parriedcreature);
        parriedcreature.StartCoroutine(CooldownRoutine(parriedcreature));
    }
    
    private IEnumerator CooldownRoutine(Creature target)
    {
        yield return Yielders.ForRealSeconds(creatureCooldown);
        seenCreatures.Remove(target);
    }
}