using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[assembly: RegisterGenericComponentType(typeof(EntityPrefab<ChunkTag>))]
public class ChunkAuthoring : EntityPrefab
{
    public override void SetPrefabType(PrefabBaker baker, Entity prefabManager)
    {
        baker.AddComponent(prefabManager, new EntityPrefab<ChunkTag>()
        {
            Value = baker.GetEntity(gameObject, TransformUsageFlags.None)
        });
    }
}

public class ChunkBaker : Baker<ChunkAuthoring>
{
    public override void Bake(ChunkAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent<ChunkTag>(entity);
        AddComponent<TileAbstractData>(entity);
        AddComponent<TilePositionData>(entity);
        AddComponent(entity, TileNeighboursIndices.Null);
        AddComponent<TileNeighboursEntities>(entity);
        AddComponent<CellReferenceBuffer>(entity);
    }
}
