using System.Linq;
using ThunderRoad;
using ThunderRoad.Pools;
using ThunderRoad.Skill.Spell;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Enrichments;

public class EnrichmentBoltPylons : EnrichmentData
{
    public float shockDelay = 5f;
    public float shockRadius = 5f;
    public float speedMultiplier = 0.05f;
    public bool requireUnpenetrateToReset;
    public string pylonEffectId;
    public string statusId;
    
    public SkillThunderbolt skillThunderbolt;
    private EffectData pylonEffectData;
    private StatusData statusData;

    public override void OnCatalogRefresh()
    {
        base.OnCatalogRefresh();
        statusData = Catalog.GetData<StatusData>(statusId);
        pylonEffectData = Catalog.GetData<EffectData>(pylonEffectId);
        skillThunderbolt = Catalog.GetData<SkillThunderbolt>("Thunderbolt");
    }

    public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemImbued(item, imbue, spellCastCharge);
        if (spellCastCharge is not SpellCastLightning || item.GetOrAddComponent<MaxDepthDetector>() is not MaxDepthDetector maxDepthDetector) return;
        maxDepthDetector.Activate(this, MaxDepthDetector.GetValidDamagers(imbue.colliderGroup.collisionHandler.damagers, false), requireUnpenetrateToReset: requireUnpenetrateToReset);
        maxDepthDetector.OnPenetrateMaxDepthEvent -= OnPenetrateMaxDepth;
        maxDepthDetector.OnUnpenetrateEvent -= OnUnpenetrate;
        maxDepthDetector.OnPenetrateMaxDepthEvent += OnPenetrateMaxDepth;
        maxDepthDetector.OnUnpenetrateEvent += OnUnpenetrate;
    }

    public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemUnimbued(item, imbue, spellCastCharge);
        if (imbue.spellCastBase.id != "Lightning" || !item.TryGetComponent(out MaxDepthDetector maxDepthDetector)) return;

        foreach (Creature creature in Creature.allActive)
        {
            if (creature.TryGetComponent(out BoltPylon boltPylon) && boltPylon.active && boltPylon.item == item) boltPylon.Unload();
        }
        
        maxDepthDetector.OnPenetrateMaxDepthEvent -= OnPenetrateMaxDepth;
        maxDepthDetector.OnUnpenetrateEvent -= OnUnpenetrate;
        maxDepthDetector.Deactivate(this);
    }

    private void OnPenetrateMaxDepth(Damager damager, CollisionInstance collision, Vector3 velocity, float depth)
    {
        if (collision.targetColliderGroup?.collisionHandler?.Entity is not Creature creature) return;
        Item item = collision.sourceColliderGroup.collisionHandler.item;
        creature.gameObject.GetOrAddComponent<BoltPylon>().Load(creature, item, statusData, collision, item?.imbues?.FirstOrDefault(i => i.spellCastBase.id == "Lightning")?.spellCastBase as SpellCastLightning, this);
    }
    
    private void OnUnpenetrate(Damager damager, CollisionInstance collision, Vector3 velocity, float depth)
    {
        if (collision.targetColliderGroup?.collisionHandler?.Entity is not Creature creature || !creature.gameObject.TryGetComponent(out BoltPylon boltPylon) || !boltPylon.active) return;
        boltPylon.Unload();
    }

    public class BoltPylon : ThunderBehaviour
    {
        public EnrichmentBoltPylons enrichmentBoltPylons;
        public SpellCastLightning spellCastLightning;
        public EffectInstance chargeEffectInstance;
        public EffectInstance pylonEffectInstance;
        public Transform boltTransform;
        public StatusData statusData;
        public Creature creature;
        public Item item;
        public bool active;

        private Vector3 lastOrbPosition;
        private float lastShockTime;
        public override ManagedLoops EnabledManagedLoops => ManagedLoops.Update;

        public void Load(Creature creature, Item item, StatusData statusData, CollisionInstance collisionInstance, SpellCastLightning spellCastLightning, EnrichmentBoltPylons enrichmentBoltPylons)
        {
            this.enrichmentBoltPylons = enrichmentBoltPylons;
            this.spellCastLightning = spellCastLightning;
            this.statusData = statusData;
            this.creature = creature;
            this.item = item;

            boltTransform = PoolUtils.GetTransformPoolManager().Get();
            
            creature.SetPhysicModifier(this, enrichmentBoltPylons.speedMultiplier, drag: 3f / enrichmentBoltPylons.speedMultiplier);
            creature.locomotion.SetAllSpeedModifiers(this, enrichmentBoltPylons.speedMultiplier);
            creature.Inflict(statusData, this, duration: Mathf.Infinity);
            active = true;

            pylonEffectInstance = enrichmentBoltPylons.pylonEffectData.Spawn(creature.GetComponentsInChildren<ColliderGroup>().ClosestToPoint(collisionInstance.contactPoint).ClosestPoint(collisionInstance.contactPoint), Quaternion.identity, collisionInstance.targetCollider.transform);
            pylonEffectInstance.Play();
        }
        
        protected override void ManagedUpdate()
        {
            base.ManagedUpdate();
            if (!active)
            {
                if (chargeEffectInstance != null)
                {
                    chargeEffectInstance.End();
                    chargeEffectInstance = null;
                    lastShockTime = 0f;
                }
                return;
            }
            if (Time.time - lastShockTime < enrichmentBoltPylons.shockDelay)
            {
                if (chargeEffectInstance == null)
                {
                    lastOrbPosition = creature.transform.position + Vector3.up * 2.5f;
                    boltTransform.position = lastOrbPosition;
                    chargeEffectInstance = spellCastLightning.chargeEffectData.Spawn(lastOrbPosition, Quaternion.identity);
                    chargeEffectInstance.Play();
                }
                chargeEffectInstance.SetIntensity(Mathf.Clamp01((Time.time - lastShockTime) / enrichmentBoltPylons.shockDelay));
                return;
            }
            chargeEffectInstance?.End();
            chargeEffectInstance = null;
            Shock(true);
        }

        public void Shock(bool chainPylons)
        {
            lastShockTime = Time.time;
            enrichmentBoltPylons.skillThunderbolt.FireBoltAt(boltTransform, creature);
            var itemPos = item.transform.position;
            foreach (Creature creature in Creature.InRadius(itemPos, enrichmentBoltPylons.shockRadius))
            {
                if (enrichmentBoltPylons.Item.owner == Item.Owner.Player && creature.isPlayer) continue;
                creature.Inflict("Electrocute", this, Random.Range(3f, 5f));
                var creaturePos = creature.GetComponentsInChildren<ColliderGroup>().ClosestToPoint(itemPos).ClosestPoint(itemPos);
                spellCastLightning.PlayBolt(itemPos, creaturePos);
                spellCastLightning.boltHitEffectData.Spawn(creaturePos, Quaternion.identity).Play();
                if (creature.TryGetComponent(out BoltPylon boltPylon) && boltPylon.active && chainPylons) boltPylon.Shock(false);
            } 
        }

        public void Unload()
        {
            creature.locomotion.RemoveSpeedModifier(this);
            creature.RemovePhysicModifier(this);
            creature.Remove(statusData, this);
            PoolUtils.GetTransformPoolManager().Release(boltTransform);
            pylonEffectInstance.End();
            active = false;
        }
    }
}