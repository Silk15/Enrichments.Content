using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace Enrichments
{
    /// <summary>
    /// Written by Phantom
    /// </summary>
    public class EnrichmentFlashpoint : EnrichmentData
    {
        public float requiredVelocity = 15f;
        public float creatureCooldown = 2f;
        public Kindling.KindlingData kindlingData;

        private List<Creature> seenCreatures = new();

        public bool IsValidTarget(Creature target, SpellCastCharge spellData) => target != null && !target.isCulled && !target.isKilled && target != spellData?.spellCaster.ragdollHand.creature && !seenCreatures.Contains(target);

        public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
        {
            base.OnItemImbued(item, imbue, spellCastCharge);
            if (spellCastCharge is not SpellCastProjectile) return;
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
            Creature target = hitCreature;
            if (hit == null || spelldata is not SpellCastProjectile || fired || eventtime != EventTime.OnEnd || !IsValidTarget(hitCreature, spelldata) || hit.impactVelocity.sqrMagnitude < requiredVelocity * requiredVelocity) return;

            if (!hit.targetCollider.attachedRigidbody.TryGetComponent(out RagdollPart ragdollPart) || ragdollPart.ragdoll.creature.isPlayer) return;

            new GameObject("Kindling").AddComponent<Kindling>().Initialize(kindlingData, hit.contactPoint, ragdollPart, hit.sourceColliderGroup.collisionHandler.item, spelldata.spellCaster.ragdollHand.creature);

            seenCreatures.Add(hitCreature);
            hitCreature.StartCoroutine(CooldownRoutine(hitCreature));
        }

        private IEnumerator CooldownRoutine(Creature target)
        {
            yield return new WaitForSecondsRealtime(creatureCooldown);
            seenCreatures.Remove(target);
        }

        public class Kindling : ThunderBehaviour
        {
            private float startTime;
            private bool ready;

            public KindlingData data;
            public RagdollPart ragdollPart;
            public Item weapon;
            public Creature ignoredCreature;
            private EffectInstance kindlingEffectInstance;
            private SphereCollider sphereCollider;

            /// <summary>
            /// Prepares a Kindling on a creature's ragdoll part, which will wait for the triggered weapon to leave the impact area, then wait for the weapon to re-enter before exploding.
            /// </summary>
            /// <param name="data">Kindling data object found in this file</param>
            /// <param name="position">position of the kindling, likely the collision point</param>
            /// <param name="ragdollPart">ragdoll part the kindling should be parented to</param>
            /// <param name="item">weapon used to create the kindling, will be checked for explosion</param>
            /// <param name="ignoredCreature">creature ignored for explosion damage and force</param>
            public void Initialize(KindlingData data, Vector3 position, RagdollPart ragdollPart, Item item, Creature ignoredCreature = null)
            {
                this.data = data;
                data.OnCatalogRefresh();
                this.ragdollPart = ragdollPart;
                weapon = item;
                this.ignoredCreature = ignoredCreature;
                transform.position = position;
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                transform.SetParent(ragdollPart.transform, true);
                kindlingEffectInstance = data.kindlingEffectData?.Spawn(transform);
                EffectInstance.EffectFinishEvent onEffectFinished = null;
                onEffectFinished = instance =>
                {
                    instance.onEffectFinished -= onEffectFinished;
                    Destroy(this);
                };
                kindlingEffectInstance?.Play();
                // Start not ready so the weapon doesn't instantly trigger the effect
                ready = false;
                sphereCollider = transform.gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = data.readyExitRadius;
                sphereCollider.isTrigger = true;
                sphereCollider.enabled = true;
                startTime = Time.time;
            }

            /// <summary>
            /// Handles duration based despawning
            /// </summary>
            public void Update()
            {
                if (data != null && Time.time - startTime <= data.duration && sphereCollider.enabled)
                    return;
                sphereCollider.enabled = false;
                kindlingEffectInstance?.End();
            }

            /// <summary>
            /// Checks for the correct weapon before triggering the explosion.
            /// </summary>
            /// <param name="other">Colliding object</param>
            public void OnTriggerEnter(Collider other)
            {
                if (!other.TryGetComponentInParent(out Item item) || item != weapon || !ready) return;

                Explode();

                if (ragdollPart.isSliced && !ragdollPart.sliceAllowed)
                    return;
                ragdollPart.SafeSlice(); // TODO: always dismembers at the torso joint, could be improved
                ragdollPart.physicBody.AddForce(ragdollPart.upDirection.normalized * data.dismemberForce, ForceMode.VelocityChange);
                if (!ragdollPart.ragdoll.creature.isKilled)
                    ragdollPart.ragdoll.creature.Kill();
            }

            /// <summary>
            /// First stage of event detection, requires the weapon to leave a larger radius than the explosion detection to prevent cases where the explosion is triggered instantly after hitting.
            /// </summary>
            /// <param name="other">Colliding object</param>
            public void OnTriggerExit(Collider other)
            {
                if (!other.TryGetComponentInParent(out Item item) || item != weapon || ready) return;
                // Initial hit has left the impact area, prep the Kindling for explosion
                sphereCollider.radius = data.detonateTriggerRadius;
                ready = true;
            }

            /// <summary>
            /// Explode, dealing damage and applying force in a radius, ignores the creature defined in Initialize, doubles as the end event of the kindling.
            /// </summary>
            public void Explode()
            {
                sphereCollider.enabled = false;
                EffectInstance explosion = data.explosionEffectData?.Spawn(transform.position, Quaternion.identity, parent: transform);
                explosion?.SetSize(data.explosionEffectScale);
                explosion?.Play();
                foreach ((ThunderEntity thunderEntity, Vector3 closestPoint) in ThunderEntity.InRadiusClosestPoint(transform.position, data.explosionRadius))
                {
                    float magnitude = (closestPoint - transform.position).magnitude;
                    switch (thunderEntity)
                    {
                        case Creature hitEntity:
                            float num = Mathf.InverseLerp(data.explosionRadius, 0.0f, magnitude);
                            if (hitEntity == ragdollPart.ragdoll.creature || (ignoredCreature != null && hitEntity == ignoredCreature))
                                break;
                            hitEntity.Damage(data.explosionDamage * num);
                            if (magnitude < data.explosionRadius)
                                hitEntity.TryPush(Creature.PushType.Magic, hitEntity.ragdoll.targetPart.transform.position - transform.position, 1);
                            hitEntity.ragdoll.targetPart.physicBody.AddExplosionForce(data.explosionForce, transform.position, data.explosionRadius, 0.5f, ForceMode.Impulse);
                            break;
                        case Item obj:
                            obj.physicBody.AddExplosionForce(data.explosionForce, transform.position, data.explosionRadius, 0.5f, ForceMode.Impulse);
                            Breakable breakable = obj.breakable;
                            if (breakable != null && !breakable.contactBreakOnly)
                            {
                                obj.breakable.Explode(data.explosionForce, transform.position, data.explosionRadius, 0.0f, ForceMode.Impulse);
                            }

                            break;
                    }
                }

                kindlingEffectInstance?.End();
            }

            public class KindlingData : CustomData
            {
                public string kindlingEffectId;
                public string explosionEffectId;
                public float duration = 1.5f;
                public float readyExitRadius = 0.5f;
                public float detonateTriggerRadius = 0.1f;
                public float explosionDamage = 10f;
                public float explosionForce = 3f;
                public float explosionRadius = 1.5f;
                public float explosionEffectScale = 0.3f;
                public float dismemberForce = 3f;

                public EffectData kindlingEffectData;
                public EffectData explosionEffectData;

                public override void OnCatalogRefresh()
                {
                    base.OnCatalogRefresh();
                    kindlingEffectData = Catalog.GetData<EffectData>(kindlingEffectId);
                    explosionEffectData = Catalog.GetData<EffectData>(explosionEffectId);
                }
            }
        }
    }
}