using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PrefabAuthoring : MonoBehaviour
{
    public EntityPrefab[] prefabs;
}

public class PrefabBaker : Baker<PrefabAuthoring>
{
    public override void Bake(PrefabAuthoring authoring)
    {
        Entity prefabManager = GetEntity(TransformUsageFlags.None);
        for (int i = 0; i < authoring.prefabs.Length; i++)
        {
            EntityPrefab prefab = authoring.prefabs[i];
            prefab.SetPrefabType(this, prefabManager);
        }
    }
}

public abstract class EntityPrefab : MonoBehaviour
{
    public abstract void SetPrefabType(PrefabBaker baker, Entity prefabManager);
}

public struct EntityPrefab<T> : IComponentData where T : unmanaged, IComponentData
{
    public Entity Value;

    public static implicit operator Entity(EntityPrefab<T> v) { return v.Value; }
    public static implicit operator EntityPrefab<T>(Entity v) { return new EntityPrefab<T> { Value = v }; }
}
