using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[assembly: RegisterGenericComponentType(typeof(EntityPrefab<CellTag>))]

public class CellAuthoring : EntityPrefab
{
    public override void SetPrefabType(PrefabBaker baker, Entity prefabManager)
    {
        baker.AddComponent(prefabManager, new EntityPrefab<CellTag>()
        {
            Value = baker.GetEntity(gameObject, TransformUsageFlags.None)
        });
    }

}

public class CellBaker : Baker<CellAuthoring>
{
    public override void Bake(CellAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        AddComponent<CellTag>(entity);
        AddComponent<GridReference>(entity);
        AddComponent<TileAbstractData>(entity);
        AddComponent<TilePositionData>(entity);
        AddComponent(entity, TileNeighboursIndices.Null);
        AddComponent<TileNeighboursEntities>(entity);
        AddComponent(entity, ChunkNeighboursIndices.Null);
        AddComponent<ChunkParentReference>(entity);
    }
}