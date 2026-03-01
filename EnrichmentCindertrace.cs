using System;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using TriInspector;
using UnityEngine;

namespace Enrichments;

public class EnrichmentCindertrace : EnrichmentData
{
    public float minHorizontalVelocity = 0.5f;
    
    [Dropdown(nameof(GetAllSpellID))]
    public string spellId = "Fire";
    
    [NonSerialized]
    public EffectData dragEffectData;
    
    [Dropdown(nameof(GetAllEffectID))]
    public string dragEffectId = "CindertraceDrag";
    
    protected int spellHashId;
    
    #if !SDK
    
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
    #endif

    public class Cindertrace : ThunderBehaviour
    {
        [NonSerialized]
        public EnrichmentCindertrace enrichmentCindertrace;
        
        [NonSerialized]
        public Item item;
        
        private bool canRun;
        private float lastFlamewallTime;
        private float flamewallCooldown = 0.175f;
        private Vector3 flamewallOffset = new(0, 0.3f, 0);
        private SkillTwinFlame flamewallSkillData;
        
        #if !SDK
        public void Enable(Item item, EnrichmentCindertrace enrichmentCindertrace)
        {
            flamewallSkillData = Catalog.GetData<SkillTwinFlame>("TwinFlame");
            this.enrichmentCindertrace = enrichmentCindertrace;
            this.item = item;
            canRun = true;
        }

        public void Disable() => canRun = false;

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
            flamewall.Init(flamewallSkillData);
            flamewall.transform.localScale *= 0.4f;
        }
        #endif
    }
}