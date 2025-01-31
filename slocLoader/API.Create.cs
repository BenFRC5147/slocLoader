﻿using Mirror;
using slocLoader.ObjectCreation;
using slocLoader.Objects;
using slocLoader.TriggerActions;
using slocLoader.TriggerActions.Data;
using slocLoader.TriggerActions.Enums;
using slocLoader.TriggerActions.Handlers;

namespace slocLoader;

public static partial class API
{

    public static slocGameObject CreateDefaultObject(this ObjectType type) => type switch
    {
        ObjectType.Cube
            or ObjectType.Sphere
            or ObjectType.Cylinder
            or ObjectType.Plane
            or ObjectType.Capsule
            or ObjectType.Quad => new PrimitiveObject(0, type),
        ObjectType.Light => new LightObject(0),
        ObjectType.Empty => new EmptyObject(0),
        _ => null
    };

    public static GameObject CreateObject(this slocGameObject obj, GameObject parent = null, bool throwOnError = true)
    {
        var transform = obj.Transform;
        return obj switch
        {
            PrimitiveObject primitive => CreatePrimitive(parent, primitive, transform),
            LightObject light => CreateLight(parent, transform, light),
            EmptyObject => CreateEmpty(parent, transform),
            _ => throwOnError ? throw new IndexOutOfRangeException($"Unknown object type {obj.Type}") : null
        };
    }

    private static GameObject CreatePrimitive(GameObject parent, PrimitiveObject primitive, slocTransform transform)
    {
        if (PrimitivePrefab == null)
            throw new InvalidOperationException("Primitive prefab is not set! Make sure to spawn objects after the prefabs have been loaded.");
        var toy = Object.Instantiate(PrimitivePrefab);
        var colliderMode = primitive.GetNonUnsetColliderMode();
        var primitiveType = primitive.Type.ToPrimitiveType();
        var o = toy.gameObject;
        var sloc = o.AddComponent<slocObjectData>();
        sloc.HasColliderOnClient = colliderMode is PrimitiveObject.ColliderCreationMode.ClientOnly or PrimitiveObject.ColliderCreationMode.Both;
        if (colliderMode is PrimitiveObject.ColliderCreationMode.NonSpawnedTrigger or PrimitiveObject.ColliderCreationMode.ServerOnlyNonSpawned or PrimitiveObject.ColliderCreationMode.NoColliderNonSpawned)
            sloc.ShouldBeSpawnedOnClient = false;
        if (colliderMode is not (PrimitiveObject.ColliderCreationMode.NoCollider or PrimitiveObject.ColliderCreationMode.ClientOnly or PrimitiveObject.ColliderCreationMode.NoColliderNonSpawned))
            o.AddProperCollider(primitiveType, colliderMode.IsTrigger());
        AddActionHandlers(o, primitive);
        toy.PrimitiveType = primitiveType;
        toy.MovementSmoothing = primitive.MovementSmoothing;
        toy.SetAbsoluteTransformFrom(parent);
        toy.SetLocalTransform(transform);
        toy.Scale = AdminToyPatch.GetScale(transform.Scale, sloc.HasColliderOnClient);
        toy.MaterialColor = primitive.MaterialColor;
        return o;
    }

    private static GameObject CreateLight(GameObject parent, slocTransform transform, LightObject light)
    {
        if (LightPrefab == null)
            throw new InvalidOperationException("Light prefab is not set! Make sure to spawn objects after the prefabs have been loaded.");
        var toy = Object.Instantiate(LightPrefab);
        toy.SetAbsoluteTransformFrom(parent);
        toy.SetLocalTransform(transform);
        toy.LightColor = light.LightColor;
        toy.LightShadows = light.Shadows;
        toy.LightRange = light.Range;
        toy.LightIntensity = light.Intensity;
        toy.Scale = transform.Scale;
        toy.MovementSmoothing = light.MovementSmoothing;
        return toy.gameObject;
    }

    private static GameObject CreateEmpty(GameObject parent, slocTransform transform)
    {
        var emptyObject = new GameObject("Empty", typeof(NetworkIdentity));
        emptyObject.SetAbsoluteTransformFrom(parent);
        emptyObject.SetLocalTransform(transform);
        return emptyObject;
    }

    private static void AddActionHandlers(GameObject o, PrimitiveObject primitive)
    {
        if (primitive.TriggerActions is not {Length: not 0})
            return;
        var enter = new List<HandlerDataPair>();
        var stay = new List<HandlerDataPair>();
        var exit = new List<HandlerDataPair>();
        foreach (var action in primitive.TriggerActions)
        {
            if (action is SerializableTeleportToSpawnedObjectData tp)
                TpToSpawnedCache.GetOrAdd(o, () => new List<SerializableTeleportToSpawnedObjectData>()).Add(tp);
            else if (ActionManager.TryGetHandler(action.ActionType, out var handler))
                AddActionDataPairToList(action, handler, enter, stay, exit);
        }

        if (enter.Count == 0 && stay.Count == 0 && exit.Count == 0)
            return;
        var component = o.AddComponent<TriggerListener>();
        component.OnEnter.AddRange(enter);
        component.OnStay.AddRange(stay);
        component.OnExit.AddRange(exit);
    }

    public static void AddActionDataPairToList(BaseTriggerActionData action, ITriggerActionHandler handler, List<HandlerDataPair> enter, List<HandlerDataPair> stay, List<HandlerDataPair> exit)
    {
        var e = action.SelectedEvents;
        if (e == TriggerEventType.None)
            return;
        var pair = new HandlerDataPair(action, handler);
        if (e.Is(TriggerEventType.Enter))
            enter.Add(pair);
        if (e.Is(TriggerEventType.Stay))
            stay.Add(pair);
        if (e.Is(TriggerEventType.Exit))
            exit.Add(pair);
    }

    private static readonly InstanceDictionary<GameObject> CreatedInstances = new();

    private static readonly Dictionary<GameObject, List<SerializableTeleportToSpawnedObjectData>> TpToSpawnedCache = new();

    #region Deprecated methods

    [Obsolete("Use CreateObjects(ObjectSource, Vector3, Quaternion) instead")]
    public static GameObject CreateObjects(IEnumerable<slocGameObject> objects, Vector3 position, Quaternion rotation = default)
        => CreateObjects(objects, out _, position, rotation);

    [Obsolete("Use CreateObjects(ObjectSource, out int, Vector3, Quaternion) instead")]
    public static GameObject CreateObjects(IEnumerable<slocGameObject> objects, out int createdAmount, Vector3 position, Quaternion rotation = default)
        => CreateObjects(ObjectsSource.From(objects), new CreateOptions
        {
            Position = position,
            Rotation = rotation
        }, out createdAmount);

    [Obsolete("Use CreateObjects(ObjectSource, out int, Vector3, Quaternion) instead")]
    public static GameObject CreateObjectsFromStream(Stream objects, out int createdAmount, Vector3 position, Quaternion rotation = default)
        => CreateObjects(objects, new CreateOptions
        {
            Position = position,
            Rotation = rotation
        }, out createdAmount);

    [Obsolete("Use CreateObjects(ObjectSource, out int, Vector3, Quaternion) instead")]
    public static GameObject CreateObjectsFromFile(string path, out int createdAmount, Vector3 position, Quaternion rotation = default)
        => CreateObjects(path, new CreateOptions
        {
            Position = position,
            Rotation = rotation
        }, out createdAmount);

    #endregion

    public static GameObject CreateObjects(ObjectsSource source, CreateOptions options, out int createdAmount)
        => CreateOrSpawn(source, options, false, CreateObject, out createdAmount);

    public static GameObject CreateObjects(ObjectsSource source, CreateOptions options)
        => CreateObjects(source, options, out _);

    public static GameObject CreateObjects(ObjectsSource source, out int createdAmount, Vector3 position, Quaternion rotation = default)
        => CreateObjects(source, new CreateOptions
        {
            Position = position,
            Rotation = rotation
        }, out createdAmount);

    public static GameObject CreateObjects(ObjectsSource source, Vector3 position, Quaternion rotation = default)
        => CreateObjects(source, out _, position, rotation);

    private static void PostProcessSpecialTriggerActions()
    {
        if (!ActionManager.TryGetHandler(TriggerActionType.TeleportToSpawnedObject, out var handler))
            return;
        foreach (var kvp in TpToSpawnedCache)
        foreach (var data in kvp.Value)
        {
            if (CreatedInstances.TryGetValue(data.ID, out var target))
                kvp.Key.AddTriggerAction(new RuntimeTeleportToSpawnedObjectData(target, data.Offset)
                {
                    SelectedTargets = data.SelectedTargets,
                    Options = data.Options
                }, handler);
        }
    }

}
