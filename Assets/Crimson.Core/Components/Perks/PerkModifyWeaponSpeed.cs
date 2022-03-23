﻿using System;
using System.Collections.Generic;
using System.Linq;
using Crimson.Core.Common;
using Crimson.Core.Enums;
using Crimson.Core.Loading;
using Crimson.Core.Utils;
using Sirenix.OdinInspector;
using Unity.Entities;
using UnityEngine;

namespace Crimson.Core.Components.Perks
{
    public class PerkModifyWeaponSpeed : MonoBehaviour, IActorAbilityTarget, IPerkAbilityForSpawned
    {
        public bool ApplyToAllProjectiles = true;

        [HideIf("ApplyToAllProjectiles")] public string componentName = "";

        public float weaponSpeedModifier;
        public CollisionSettings collisionSettings;
        
        public  bool spawnTargetEffect;
        
        [ShowIf("spawnTargetEffect")]
        public GameObject targetEffectPrefab;

        public IActor Actor { get; set; }
        public IActor TargetActor { get; set; }
        public IActor AbilityOwnerActor { get; set; }

        private EntityManager _dstManager;

        public void AddComponentData(ref Entity entity, IActor actor)
        {
            Actor = actor;
            _dstManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        public void Execute()
        {
            if (spawnTargetEffect)
            {
                SpawnEffect(TargetActor);
            }
        }
        
        public void AddCollision(GameObject target)
        {
            var collision = target.GetComponent<AbilityCollision>();
            
            if (collision != null)
            {
                collision.collisionActions.Add(new CollisionAction
                {
                    collisionLayerMask = collisionSettings.collisionLayerMask,
                    useTagFilter = collisionSettings.useTagFilter,
                    filterMode = collisionSettings.filterMode,
                    filterTags = collisionSettings.filterTags,
                    executeOnCollisionWithSpawner = collisionSettings.executeOnCollisionWithSpawner,
                    destroyAfterAction = collisionSettings.destroyAfterAction,
                    actions = new List<MonoBehaviour>{this}
                });
            }
        }

        public void Apply(IActor target)
        {
            var copy = target.GameObject.CopyComponent(this) as PerkModifyWeaponSpeed;
            
            if (copy == null)
            {
                Debug.LogError("[PERK MODIFY WEAPON SPEED] Error copying perk to Actor!");
                return;
            }
            var e = target.ActorEntity;
            copy.AddComponentData(ref e,target);
            
            if (!Actor.Spawner.AppliedPerks.Contains(copy)) Actor.Spawner.AppliedPerks.Add(copy);
            
            var projectiles = target.GameObject.GetComponents<AbilityWeapon>().ToList();
            
            if (!ApplyToAllProjectiles)
                projectiles = projectiles
                    .Where(p => p.ComponentName.Equals(componentName, StringComparison.Ordinal))
                    .ToList();
            
            foreach (var p in projectiles)
            {
                p.SpawnCallbacks.Add(copy.ApplyWeaponSpeedModifier);
                p.SpawnCallbacks.Add(copy.AddCollisionAction);
            }
        }

        public void AddCollisionAction(GameObject target)
        {
            var p = target.CopyComponent(this) as PerkModifyWeaponSpeed;
            if (p == null) return;
            
            var a = target.GetComponent<IActor>();
            if (a != null)
            {
                var e = a.ActorEntity;
                p.AddComponentData(ref e,a);
            }
            
            p.AddCollision(p.gameObject);
        }

        private void ApplyWeaponSpeedModifier(GameObject target)
        {
            var targetActor = target.GetComponent<IActor>();
            
            if (targetActor == null) return;
            
            var movementData = _dstManager.GetComponentData<ActorMovementData>(targetActor.ActorEntity);
            movementData.ExternalMultiplier = weaponSpeedModifier;

            _dstManager.SetComponentData(targetActor.ActorEntity, movementData);
            
            var targetLifespan = (AbilityLifespan) targetActor.Abilities.FirstOrDefault(ability => ability is AbilityLifespan);

            if (targetLifespan == null || Math.Abs(weaponSpeedModifier) < 0.001f)
                return;
            
            targetLifespan.lifespan /= weaponSpeedModifier;
            targetLifespan.Timer.TimedActions.Clear();
            targetLifespan.Execute();
        }

        private void SpawnEffect(IActor target)
        {
            var effectData = new ActorSpawnerSettings
            {
                objectsToSpawn = new List<GameObject> {targetEffectPrefab},
                SpawnPosition = SpawnPosition.UseSpawnerPosition,
                parentOfSpawns = TargetType.None,
                runSpawnActionsOnObjects = true,
                destroyAbilityAfterSpawn = true
            };
            
            ActorSpawn.Spawn(effectData, target, Actor);
        }
        
        public void Remove()
        {
            if (Actor.Spawner.AppliedPerks.Contains(this)) Actor.Spawner.AppliedPerks.Remove(this);
            Destroy(this);
        }
    }
}