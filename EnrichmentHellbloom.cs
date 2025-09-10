using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments
{
    /// <summary>
    /// Written by Phantom
    /// </summary>
    public class EnrichmentHellbloom : EnrichmentData
    {
        public float depthRequirementRatio = 0.9f;
        public float eventResetRatio = 0.2f;
        public bool requireUnpenetrateToReset = true;
        public bool allowSlash = true;
        public bool allowIgnited = true;

        private StatusDataBurning statusData;

        public bool IsValidTarget(Creature target, SpellCastCharge spellData) => target != null && !target.isCulled && target != spellData?.spellCaster.ragdollHand.creature;

        public override void OnCatalogRefresh()
        {
            base.OnCatalogRefresh();
            statusData = Catalog.GetData<StatusDataBurning>("Burning");
        }

        public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
        {
            base.OnItemImbued(item, imbue, spellCastCharge);
            if (spellCastCharge is not SpellCastProjectile) return;
            MaxDepthDetector maxDepthDetector = item.GetOrAddComponent<MaxDepthDetector>();
            if (maxDepthDetector == null) return;
            List<Damager> damagers = MaxDepthDetector.GetValidDamagers(imbue.colliderGroup.collisionHandler.damagers, allowSlash);
            if (damagers.Count <= 0) return;
            maxDepthDetector.Activate(this, damagers, depthRequirementRatio, eventResetRatio, requireUnpenetrateToReset);
            maxDepthDetector.OnPenetrateMaxDepthEvent -= OnPenetrateMaxDepthEvent;
            maxDepthDetector.OnPenetrateMaxDepthEvent += OnPenetrateMaxDepthEvent;
        }

        public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
        {
            base.OnItemUnimbued(item, imbue, spellCastCharge);
            MaxDepthDetector maxDepthDetector = item.GetComponent<MaxDepthDetector>();
            if (maxDepthDetector == null) return;
            maxDepthDetector.Deactivate(this);
            maxDepthDetector.OnPenetrateMaxDepthEvent -= OnPenetrateMaxDepthEvent;
        }

        private void OnPenetrateMaxDepthEvent(Damager damager, CollisionInstance collision, Vector3 velocity, float depth)
        {
            Creature hitCreature = collision?.targetColliderGroup?.collisionHandler?.ragdollPart?.ragdoll?.creature;
            if (collision == null || damager.colliderGroup.imbue.spellCastBase is not SpellCastProjectile spellCastFire || !IsValidTarget(hitCreature, spellCastFire)) return;

            if (!hitCreature.TryGetStatus(statusData, out Burning status) || (status.isIgnited && !allowIgnited)) return;
            status.Heat = statusData.maxHeat;
            FlameWall.Create(hitCreature.ragdoll.targetPart.transform.position).Init(Catalog.GetData<SkillTwinFlame>("TwinFlame"));
        }
    }
}