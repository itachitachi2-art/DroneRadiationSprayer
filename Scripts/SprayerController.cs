using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Itachi.DroneRadiationSprayer
{
    internal static class SprayerController
    {
        internal const string AttachmentName = "modDroneRadiationSprayer";
        internal const string InhibitorName = "buffRadiatedRegenBlock15";
        internal const float Radius = 25f;
        internal const float PulseInterval = 1f;

        private sealed class DroneState
        {
            internal float NextPulse;
            internal float LastTime;
            internal readonly List<Entity> Candidates = new List<Entity>();
        }

        private sealed class TargetState
        {
            internal float NextRefresh;
            internal float LastTime;
        }

        // Weak entity keys avoid retaining drones/zombies across unloads, pickup,
        // save changes or world changes. Reused IDs cannot inherit old cooldowns.
        private static ConditionalWeakTable<EntityDrone, DroneState> drones;
        private static ConditionalWeakTable<EntityZombie, TargetState> targets;
        private static FastTags<TagGroup.Global> eligibleTags;
        private static float nextErrorLog;

        static SprayerController() { Reset(); }

        internal static void Reset()
        {
            drones = new ConditionalWeakTable<EntityDrone, DroneState>();
            targets = new ConditionalWeakTable<EntityZombie, TargetState>();
            eligibleTags = FastTags<TagGroup.Global>.Parse("radiated,charged,infernal");
            nextErrorLog = 0f;
        }

        internal static void Tick(EntityDrone drone)
        {
            // This postfix must not break the original update during unload or
            // when a third-party entity implementation throws.
            float now = Time.time;
            if (float.IsNaN(now) || float.IsInfinity(now)) return;
            try
            {
                if (!CanSpray(drone)) return;
                DroneState state = drones.GetValue(drone, CreateDroneState);
                if (now < state.LastTime) state.NextPulse = now;
                state.LastTime = now;
                if (now < state.NextPulse) return;
                state.NextPulse = now + PulseInterval;

                // Inspect only installed modifications on this drone, never its
                // cargo. Removing the attachment stops subsequent pulses.
                if (!HasAttachment(drone.OriginalItemValue)) return;

                try
                {
                    drone.world.GetEntitiesAround(EntityFlags.Zombie, drone.position, Radius, state.Candidates);
                    for (int i = 0; i < state.Candidates.Count; i++)
                    {
                        EntityZombie zombie = state.Candidates[i] as EntityZombie;
                        if (!IsEligibleTarget(drone, zombie)) continue;
                        TargetState targetState = targets.GetValue(zombie, CreateTargetState);
                        if (now >= targetState.LastTime && now < targetState.NextRefresh) continue;
                        targetState.LastTime = now;
                        targetState.NextRefresh = now + PulseInterval;

                        // -1 preserves the vanilla shared BuffClass duration.
                        // netSync=true uses vanilla server buff replication.
                        // No damage, attack target, revenge target or wake call.
                        zombie.Buffs.AddBuff(InhibitorName, -1, true, false, -1f);
                    }
                }
                finally
                {
                    // Do not keep nearby entities strongly referenced between scans.
                    state.Candidates.Clear();
                }
            }
            catch (Exception ex)
            {
                if (now >= nextErrorLog)
                {
                    nextErrorLog = now + 30f;
                    Debug.LogWarning("[DroneRadiationSprayer] Skipped pulse: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private static DroneState CreateDroneState(EntityDrone drone) { return new DroneState(); }
        private static TargetState CreateTargetState(EntityZombie zombie) { return new TargetState(); }

        internal static bool CanSpray(EntityDrone drone)
        {
            if (drone == null || drone.world == null || drone.world.IsRemote()) return false;
            if (drone.IsDespawned || drone.markedForUnload || drone.isUnloaded || drone.IsDead()) return false;
            if (drone.isShutdown || drone.isShutdownPending || drone.isBeingPickedUp) return false;
            if (drone.Health <= 2 || drone.GetState() == EntityDrone.State.Shutdown) return false;
            EntityDrone.State mode = drone.GetState();
            if (mode != EntityDrone.State.Idle && mode != EntityDrone.State.Sentry &&
                mode != EntityDrone.State.Follow && mode != EntityDrone.State.Heal &&
                mode != EntityDrone.State.Attack) return false;
            // Health=2 is also ExcuseMeDrone's temporary F10 revive state. The
            // conservative gate is dependency-free; a genuine 2-HP drone must be
            // repaired before spraying too. No ExcuseMeDrone assembly is loaded.
            EntityPlayer owner = drone.Owner as EntityPlayer;
            if (owner == null || owner.IsDead() || owner.world != drone.world) return false;
            if (owner.IsDespawned || owner.markedForUnload || owner.isUnloaded) return false;
            return drone.world.GetEntity(drone.entityId) == drone &&
                   drone.world.GetEntity(owner.entityId) == owner;
        }

        internal static bool HasAttachment(ItemValue droneItem)
        {
            if (droneItem == null) return false;
            for (int i = 0; i < droneItem.ModificationCount; i++)
            {
                ItemValue mod = droneItem.GetModification(i);
                if (mod != null && mod.ItemClass != null && mod.ItemClass.Name == AttachmentName) return true;
            }
            return false;
        }

        internal static bool IsEligibleTarget(EntityDrone drone, EntityZombie zombie)
        {
            if (zombie == null || zombie.world != drone.world || zombie.Buffs == null || zombie.IsDead()) return false;
            if (zombie.IsDespawned || zombie.markedForUnload || zombie.isUnloaded) return false;
            if (!zombie.HasAnyTags(eligibleTags)) return false;
            return WithinRadius(drone.position, zombie.position);
        }

        internal static bool WithinRadius(Vector3 center, Vector3 target)
        {
            // Full three-dimensional sphere, including the boundary. Do not use
            // the broad-phase query as the final geometric decision.
            return (target - center).sqrMagnitude <= Radius * Radius;
        }
    }
}
