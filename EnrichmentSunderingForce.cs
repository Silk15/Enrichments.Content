using System.Collections;
using System.Collections.Generic;
using Enrichments;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments
{
    /// <summary>
    /// Written by Phantom
    /// </summary>
    public class EnrichmentSunderingForce : EnrichmentData
    {
        public string effectId;
        public float requiredVelocity = 0.5f;
        public float forceMultiplier = 2f;
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

        private void OnImbueHit(SpellCastCharge spelldata, float amount, bool fired, CollisionInstance hit, EventTime eventtime)
        {
            Creature hitCreature = hit?.targetColliderGroup?.collisionHandler?.ragdollPart?.ragdoll?.creature;
            if (hit == null || spelldata is not SpellCastGravity || fired || eventtime != EventTime.OnStart || !IsValidTarget(hitCreature, spelldata) || hit.impactVelocity.sqrMagnitude < requiredVelocity * requiredVelocity) return;
            if (!hit.targetCollider.attachedRigidbody.TryGetComponent(out RagdollPart ragdollPart) || ragdollPart.ragdoll.creature.isPlayer || ragdollPart.isSliced || !ragdollPart.sliceAllowed) return;
            ragdollPart.Slice();
            ragdollPart.physicBody.AddForce(hit.impactVelocity * forceMultiplier, ForceMode.VelocityChange);
            if (!hitCreature.isKilled) hitCreature.Kill();
            effectData?.Spawn(hit.contactPoint, Quaternion.LookRotation(hit.contactNormal), parent: ragdollPart.transform)?.Play();
            seenCreatures.Add(hitCreature);
            hitCreature.StartCoroutine(CooldownRoutine(hitCreature));
        }

        private IEnumerator CooldownRoutine(Creature target)
        {
            yield return Yielders.ForRealSeconds(creatureCooldown);
            seenCreatures.Remove(target);
        }
    }
}