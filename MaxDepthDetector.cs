using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;

namespace Enrichments
{
    /// <summary>
    /// Written by Phantom
    /// </summary>
    public class MaxDepthDetector : ThunderBehaviour
    {
        public List<Damager> damagers;

        public bool active;
        public bool hasReachedMaxDepth;
        public float depthRequirementRatio;
        public float eventResetRatio;
        public bool requireUnpenetrateToReset;

        private List<object> handlers = new();

        public event OnPenetrateMaxDepth OnPenetrateMaxDepthEvent;

        public override ManagedLoops EnabledManagedLoops => ManagedLoops.Update;

        public static List<Damager> GetValidDamagers(List<Damager> damagers, bool checkSlash = true)
        {
            List<Damager> result = damagers.Where(x => x.type == Damager.Type.Pierce).ToList();
            if (result.Count <= 0 && checkSlash)
                result = damagers.Where(x => x.type == Damager.Type.Slash).ToList();
            return result;
        }

        public void Activate(object handler, List<Damager> damagers, float depthRequirementRatio = 0.9f, float eventResetRatio = 0.2f, bool requireUnpenetrateToReset = false)
        {
            this.damagers = damagers;
            this.depthRequirementRatio = depthRequirementRatio;
            this.eventResetRatio = eventResetRatio;
            this.requireUnpenetrateToReset = requireUnpenetrateToReset;
            foreach (Damager damager in damagers)
            {
                damager.OnUnpenetrateEvent -= OnUnpenetrateEvent;
                damager.OnUnpenetrateEvent += OnUnpenetrateEvent;
            }

            active = true;
            handlers.Add(handler);
        }

        public void Deactivate(object handler)
        {
            if (!damagers.IsNullOrEmpty()) foreach (Damager damager in damagers) 
                damager.OnUnpenetrateEvent -= OnUnpenetrateEvent;
            active = false;
            if (handlers.Contains(handler)) handlers.Remove(handler);
            if (handlers.IsNullOrEmpty()) Destroy(this);
        }

        protected virtual float GetTriggerThreshold(float maxDepth) => maxDepth * depthRequirementRatio;

        protected virtual float GetResetThreshold(float maxDepth) => GetTriggerThreshold(maxDepth) * eventResetRatio;

        protected override void ManagedUpdate()
        {
            base.ManagedUpdate();
            if (!active || damagers.IsNullOrEmpty())
                return;
            foreach (Damager damager in damagers)
            foreach (CollisionInstance collision in damager.collisionHandler.collisions)
            {
                if (collision.damageStruct.damager != damager || collision.damageStruct.penetration == 0) continue;
                if (!hasReachedMaxDepth)
                {
                    if (!(collision.damageStruct.lastDepth >= GetTriggerThreshold(damager.penetrationDepth))) continue;

                    hasReachedMaxDepth = true;
                    OnPenetrateMaxDepthEvent?.Invoke(damager, collision, damager.collisionHandler.item.Velocity, collision.damageStruct.lastDepth);
                }
                else if (!requireUnpenetrateToReset && collision.damageStruct.lastDepth < GetResetThreshold(damager.penetrationDepth))
                    hasReachedMaxDepth = false;
            }
        }

        private void OnUnpenetrateEvent(Damager damager, CollisionInstance collision, bool wentthrough, EventTime time)
        {
            hasReachedMaxDepth = false;
        }

        public delegate void OnPenetrateMaxDepth(Damager damager, CollisionInstance collision, Vector3 velocity, float depth);
    }
}