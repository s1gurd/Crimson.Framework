using Crimson.Core.Common;
using Crimson.Core.Components;
using Crimson.Core.Enums;
using Crimson.Core.Utils;
using Crimson.Core.Utils.LowLevel;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Crimson.Core.Systems
{
    [UpdateInGroup(typeof(FixedUpdateGroup))]
    public class ActorCollisionSystem : ComponentSystem
    {
        private EntityQuery _collisionQuery;
        private EntityQuery _raycastQuery;
        private EntityQuery _networkQuery;
        private EntityQuery _actorsQuery;

        private Collider[] _results = new Collider[Constants.COLLISION_BUFFER_CAPACITY];
        private Dictionary<int, List<int>> _networkCollisions = new Dictionary<int, List<int>>();
        private Dictionary<int, IActor> _localColliders = new Dictionary<int, IActor>();
        private RaycastHit[] _raycastResults = new RaycastHit[Constants.COLLISION_BUFFER_CAPACITY];

        protected override void OnCreate()
        {
            _collisionQuery = GetEntityQuery(
                typeof(AbilityCollision),
                ComponentType.ReadOnly<ActorColliderData>(),
                ComponentType.ReadOnly<Transform>());
            _raycastQuery = GetEntityQuery(
                typeof(AbilityRaycast),
                ComponentType.ReadOnly<ActorColliderData>(),
                ComponentType.ReadOnly<Transform>());
            _networkQuery = GetEntityQuery(ComponentType.ReadOnly<CollisionReceiveData>());
            _actorsQuery = GetEntityQuery(ComponentType.ReadOnly<ActorData>());
        }

        protected override void OnUpdate()
        {
            var dstManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            _networkCollisions.Clear();
            Entities.With(_networkQuery).ForEach((Entity entity, ref CollisionReceiveData collision) =>
            {
                if (_networkCollisions.ContainsKey(collision.ActorStateId))
                {
                    _networkCollisions[collision.ActorStateId].Add(collision.HitStateId);
                }
                else
                {
                    _networkCollisions.Add(collision.ActorStateId, new List<int> { collision.HitStateId });
                }

                PostUpdateCommands.DestroyEntity(entity);
            });

            _localColliders.Clear();

            if (_networkCollisions.Count > 0)
            {
                Entities.With(_actorsQuery).ForEach((Entity entity, ref ActorData id) =>
                {
                    var actor = dstManager.GetComponentObject<Actor>(entity);
                    if (actor != null) _localColliders.Add(id.StateId, actor);
                });
            }
            Entities.With(_raycastQuery).ForEach(
                (Entity entity, AbilityRaycast abilityRaycast, ref ActorColliderData colliderData) =>
                {
                    var gameObject = abilityRaycast.gameObject;
                    bool destroyAfterActions = false;

                    Debug.DrawRay(abilityRaycast.RaySource, abilityRaycast.RayDirection, Color.green);
                    var hits = Physics.RaycastNonAlloc(abilityRaycast.RaySource,
                                                       abilityRaycast.RayDirection,
                                                       _raycastResults,
                                                       colliderData.RayDistance);

                    if (hits == 0 && !_networkCollisions.ContainsKey(abilityRaycast.Actor.ActorStateId))
                    {
                        return;
                    }

                    _networkCollisions.TryGetValue(abilityRaycast.Actor.ActorStateId, out var receivedCollisions);
                    var networkCollisionActors = new List<IActor>();
                    if (receivedCollisions != null)
                        for (var i = 0; i < receivedCollisions.Count; i++)
                        {
                            var col = receivedCollisions[i];
                            _localColliders.TryGetValue(col, out var hitActor);
                            if (hitActor != null) networkCollisionActors.Add(hitActor);
                        }
                    for (var i = 0; i <= hits; i++)
                    {
                        Collider hit;
                        IActor hitActor;
                        if (i == hits && networkCollisionActors.Count > 0)
                        {
                            hitActor = networkCollisionActors.First();
                            hit = hitActor.GameObject.GetComponent<Collider>();
                            i--;
                            networkCollisionActors.RemoveAt(0);
                        }
                        else if (i < hits)
                        {
                            hit = _raycastResults[i].collider;
                            hitActor = hit.GetComponent<IActor>();
                        }
                        else continue;

                        for (var actionIndex = 0; actionIndex < abilityRaycast.collisionActions.Count; actionIndex++)
                        {
                            var action = abilityRaycast.collisionActions[actionIndex];
                            if (!action.collisionLayerMask.Contains(hit.gameObject.layer)) continue;
                            if (action.useTagFilter)
                            {
                                switch (action.filterMode)
                                {
                                    case TagFilterMode.IncludeOnly:
                                        if (!action.filterTags.Contains(hit.gameObject.tag)) continue;
                                        break;
                                    case TagFilterMode.Exclude:
                                        if (action.filterTags.Contains(hit.gameObject.tag)) continue;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            for (var itemIndex = 0; itemIndex < action.actions.Count; itemIndex++)
                            {
                                var item = action.actions[itemIndex];
                                switch (item)
                                {
                                    case IActorAbilityTarget exchange:
                                        exchange.TargetActor = hitActor;
                                        exchange.AbilityOwnerActor = abilityRaycast.Actor.Owner;
                                        exchange.Execute();
                                        break;
                                    case IActorAbility ability:
                                        ability.Execute();
                                        break;
                                }
                            }

                            if (action.destroyAfterAction) destroyAfterActions = true;
                        }
                    }

                    if (destroyAfterActions)
                    {
                        PostUpdateCommands.AddComponent<ImmediateActorDestructionData>(entity);
                    }
                });

            Entities.With(_collisionQuery).ForEach(
                (Entity entity, AbilityCollision abilityCollision, ref ActorColliderData colliderData) =>
                {
                    var gameObject = abilityCollision.gameObject;
                    float3 position = gameObject.transform.position;
                    Quaternion rotation = gameObject.transform.rotation;
                    bool destroyAfterActions = false;

                    int size = 0;

                    switch (colliderData.ColliderType)
                    {
                        case ColliderType.Sphere:
                            size = Physics.OverlapSphereNonAlloc(colliderData.SphereCenter + position,
                                colliderData.SphereRadius, _results);
                            break;
                        case ColliderType.Capsule:
                            var center =
                                ((colliderData.CapsuleStart + position) + (colliderData.CapsuleEnd + position)) / 2f;
                            var point1 = colliderData.CapsuleStart + position;
                            var point2 = colliderData.CapsuleEnd + position;
                            point1 = (float3)(rotation * (point1 - center)) + center;
                            point2 = (float3)(rotation * (point2 - center)) + center;
                            size = Physics.OverlapCapsuleNonAlloc(point1,
                                point2,
                                colliderData.CapsuleRadius, _results);
                            break;
                        case ColliderType.Box:
                            size = Physics.OverlapBoxNonAlloc(colliderData.BoxCenter + position,
                                colliderData.BoxHalfExtents, _results, colliderData.BoxOrientation * rotation);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (size == 0 && !_networkCollisions.ContainsKey(abilityCollision.Actor.ActorStateId))
                    {
                        abilityCollision.ExistentCollisions.Clear();
                        return;
                    }

                    int selfHit = 0;

                    if (abilityCollision.SpawnerColliders == null)
                    {
                        var spawner = abilityCollision.Actor.Spawner;
                        if ((spawner == null) ||
                        ((abilityCollision.SpawnerColliders = spawner.GameObject.GetAllColliders()) == null))
                        {
                            abilityCollision.SpawnerColliders = new List<Collider>();
                        }
                    }

                    if (abilityCollision.OwnColliders == null &&
                        (abilityCollision.OwnColliders = abilityCollision.gameObject.GetAllColliders()) == null)
                        return;

                    _networkCollisions.TryGetValue(abilityCollision.Actor.ActorStateId, out var receivedCollisions);
                    var networkCollisionActors = new List<IActor>();
                    if (receivedCollisions != null)
                        foreach (var col in receivedCollisions)
                        {
                            _localColliders.TryGetValue(col, out var hitActor);
                            if (hitActor != null) networkCollisionActors.Add(hitActor);
                        }

                    for (var i = 0; i <= size; i++)
                    {
                        Collider hit;
                        IActor hitActor;
                        if (i == size && networkCollisionActors.Count > 0)
                        {
                            hitActor = networkCollisionActors.First();
                            hit = hitActor.GameObject.GetComponent<Collider>();
                            i--;
                            networkCollisionActors.RemoveAt(0);
                        }
                        else if (i < size)
                        {
                            hit = _results[i];
                            hitActor = hit.GetComponent<IActor>();
                        }
                        else continue;



                        if (abilityCollision.OwnColliders.Count > 0 &&
                            abilityCollision.OwnColliders.FirstOrDefault(c => c == hit)) continue;

                        if (colliderData.initialTakeOff)
                        {
                            if (abilityCollision.SpawnerColliders.Count > 0 &&
                                abilityCollision.SpawnerColliders.FirstOrDefault(c => c == hit))
                            {
                                selfHit++;
                                continue;
                            }
                        }

                        if (abilityCollision.debugCollisions && Application.isEditor)
                        {
                            Debug.Log($"[COLLISION] HIT] {hit.gameObject} into {abilityCollision.Actor.GameObject} and collision exists: {abilityCollision.ExistentCollisions.Contains(hit)}");
                        }

                        if (abilityCollision.ExistentCollisions.Exists(c => c == hit)) continue;

                        abilityCollision.ExistentCollisions.Add(hit);

                        /*
                        if (dstManager.HasComponent<NetworkSyncSend>(abilityCollision.Actor.ActorEntity) &&
                            !dstManager.HasComponent<NetworkInputData>(abilityCollision.Actor.ActorEntity))
                        {
                            var e = dstManager.CreateEntity();
                            dstManager.AddComponentData(e, new CollisionSendData
                            {
                                ActorStateId = abilityCollision.Actor.ActorStateId,
                                HitStateId = hitActor?.ActorStateId ?? 0
                            });
                        }*/

                        foreach (var action in abilityCollision.collisionActions)
                        {
                            if (!action.collisionLayerMask.Contains(hit.gameObject.layer)) continue;
                            if (action.useTagFilter)
                            {
                                switch (action.filterMode)
                                {
                                    case TagFilterMode.IncludeOnly:
                                        if (!action.filterTags.Contains(hit.gameObject.tag)) continue;
                                        break;
                                    case TagFilterMode.Exclude:
                                        if (action.filterTags.Contains(hit.gameObject.tag)) continue;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }

                            if (!action.executeOnCollisionWithSpawner &&
                                abilityCollision.SpawnerColliders.Contains(hit))
                                continue;


                            foreach (var a in action.actions)
                            {
                                switch (a)
                                {
                                    case IActorAbilityTarget exchange:
                                        exchange.TargetActor = hitActor;
                                        exchange.AbilityOwnerActor = abilityCollision.Actor.Owner;
                                        exchange.Execute();
                                        break;
                                    case IActorAbility ability:
                                        ability.Execute();
                                        break;
                                }
                            }

                            if (action.destroyAfterAction) destroyAfterActions = true;
                        }
                    }

                    if (selfHit == 0) colliderData.initialTakeOff = false;

                    if (destroyAfterActions)
                    {
                        PostUpdateCommands.AddComponent<ImmediateActorDestructionData>(entity);
                    }

                    for (var i = abilityCollision.ExistentCollisions.Count - 1; i >= 0; i--)
                    {
                        var c = abilityCollision.ExistentCollisions[i];
                        if (!_results.Contains(c))
                        {
                            abilityCollision.ExistentCollisions.RemoveAt(i);
                        }
                    }
                });
        }
    }
}