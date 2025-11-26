using System.Collections;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments;

public class EnrichmentElectrostaticPulse : EnrichmentData
{
    public string pulseEffectId;
    public float shockRadius;

    protected EffectData pulseEffectData;

    public override void OnCatalogRefresh()
    {
        base.OnCatalogRefresh();
        pulseEffectData = Catalog.GetData<EffectData>(pulseEffectId);
    }

    public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemImbued(item, imbue, spellCastCharge);
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
        var item = hit.sourceColliderGroup?.collisionHandler?.item;
        if (eventTime == EventTime.OnEnd && spellData is SpellCastLightning spellCastLightning && hit.targetColliderGroup?.collisionHandler?.Entity is Creature && !item.IsHeld() && hit.impactVelocity.sqrMagnitude > 1 * 1)
        {
            pulseEffectData?.Spawn(hit.contactPoint, Quaternion.identity).Play();
            item.StartCoroutine(ShockRoutine(spellCastLightning, hit, 12));
        }
    }

    IEnumerator ShockRoutine(SpellCastLightning spellCastLightning, CollisionInstance hit, int boltCount)
    {
        var entities = Creature.InRadius(hit.contactPoint, shockRadius);
        for (int i = 0; i < boltCount; i++)
        {
            var entity = entities[Random.Range(0, Mathf.Min(boltCount, entities.Count))];
            if (entity.isPlayer && Item.owner == Item.Owner.Player) entities.Remove(entity);
            if (entities.Count == 0) break;
            var closestPoint = entity.GetComponentsInChildren<ColliderGroup>().ClosestToPoint(hit.contactPoint).ClosestPoint(hit.contactPoint);
            spellCastLightning.PlayBolt(sourcePos: hit.contactPoint, targetPos: closestPoint);
            spellCastLightning.boltHitEffectData.Spawn(closestPoint, Quaternion.identity).Play();
            yield return Yielders.ForSeconds(Random.Range(0.025f, 0.05f));
        }
    }
}