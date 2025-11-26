using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments;

public class EnrichmentCindertrace : EnrichmentData
{
    public float minHorizontalVelocity = 0.5f;
    public string dragEffectId = "CindertraceDrag";
    public string spellId = "Fire";

    protected int spellHashId;
    protected EffectData dragEffectData;
    
    public override void OnCatalogRefresh()
    {
        base.OnCatalogRefresh();
        spellHashId = Catalog.GetData<SpellCastProjectile>(spellId).hashId;
        dragEffectData = Catalog.GetData<EffectData>(dragEffectId);
    }

    public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemImbued(item, imbue, spellCastCharge);
        if (spellCastCharge is SpellCastProjectile spell && spell.hashId == spellHashId) item.GetOrAddComponent<Cindertrace>().Enable(item, this);
    }

    public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemUnimbued(item, imbue, spellCastCharge);
        if (spellCastCharge is SpellCastProjectile spell && spell.hashId == spellHashId && item.TryGetComponent(out Cindertrace cindertrace)) cindertrace.Disable();
    }


    public class Cindertrace : ThunderBehaviour
    {
        public EnrichmentCindertrace enrichmentCindertrace;
        public Item item;
        
        private bool canRun;
        private float lastFlamewallTime;
        private float flamewallCooldown = 0.175f;
        private Vector3 flamewallOffset = new(0, 0.3f, 0);
        
        public void Enable(Item item, EnrichmentCindertrace enrichmentCindertrace)
        {
            this.enrichmentCindertrace = enrichmentCindertrace;
            this.item = item;
            canRun = true;
        }

        public void Disable()
        {
            canRun = false;
        }

        public void OnCollisionStay(Collision other)
        {
            var horizontalVelocity = other.relativeVelocity;
            horizontalVelocity.y = 0f;
            if (!canRun || Vector3.Dot(other.GetContact(0).normal, Vector3.up) < 0.9f || other.GetContact(0).otherCollider.TryGetComponent(out ThunderEntity _) || horizontalVelocity.sqrMagnitude < enrichmentCindertrace.minHorizontalVelocity * enrichmentCindertrace.minHorizontalVelocity || Time.time - lastFlamewallTime < flamewallCooldown)
            {
                return;
            }
            lastFlamewallTime = Time.time;
            var flamewall = FlameWall.Create(other.GetContact(0).point + flamewallOffset);
            flamewall.Init(Catalog.GetData<SkillTwinFlame>("TwinFlame"));
            flamewall.transform.localScale *= 0.4f;
        }
    }
}