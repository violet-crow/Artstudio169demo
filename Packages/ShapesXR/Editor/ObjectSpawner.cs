using System;
using System.Collections.Generic;
using System.Linq;
using ShapesXR.Common;
using ShapesXR.Common.ProceduralMesh;
using ShapesXR.Common.Reactors;
using ShapesXR.Import.Initializers;
using ShapesXR.Import.Presets;
using ShapesXR.Import.Presets.Object;
using ShapesXR.Import.Presets.Special;
using ShapesXR.Import.Presets.Staging;
using ShapesXR.Import.Settings;
using ShapesXR.ImportCore.Entities;
using Ti.Common.Modules.Spaces.Models;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Properties = Ti.Common.Modules.Spaces.Props.Properties;

namespace ShapesXR.Import
{
    public class ObjectSpawner
    {
        private readonly SpaceDescriptor _spaceDescriptor;
        private readonly List<Guid> _specialObjectIds = new();
        private readonly HashSet<Guid> _groups = new();
        private readonly InitializerFactory _initializerFactory;
        private readonly List<Guid> _stateIds;
        private readonly List<Guid> _tempStatesList = new();
        
        private static Guid BackgroundStateId => Ti.Common.Modules.Spaces.Constants.Entities.BackgroundSceneStateGuid;
            
        public ObjectSpawner(SpaceDescriptor spaceDescriptor)
        {
            _spaceDescriptor = spaceDescriptor;
            _initializerFactory = new InitializerFactory();
            var spaceId = Ti.Common.Modules.Spaces.Constants.Entities.SpaceEntityGuid;
            var spaceEntity = _spaceDescriptor.Space.GetEntity<SpaceEntity>(spaceId)!;
            var sceneIds = spaceEntity.Get<Guid[]>(Properties.Space.SCENE_ORDER_GUID_ARR);
            
            var nonBackgroundSceneId = sceneIds.First(id => id != Ti.Common.Modules.Spaces.Constants.Entities.BackgroundSceneGuid);
            var nonBackgroundSceneEntity = _spaceDescriptor.Space.GetEntity<SceneEntity>(nonBackgroundSceneId)!;
            
            _stateIds = nonBackgroundSceneEntity.Get<Guid[]>(Properties.Scene.STATE_ORDER_GUID_ARR).ToList();
            
            SpawnAllObjects();
        }

        private void SpawnAllObjects()
        {
            SpawnViewpoints();
            SpawnStates();
            
            var objectCounter = 0;
            foreach (var obj in _spaceDescriptor.Objects)
            {
                var objectId = obj.Id;
                if (_specialObjectIds.Contains(objectId) || !NeedsSpawning(objectId))
                    continue;

                objectCounter++;

                var preset = _spaceDescriptor.ObjectPresets.GetValueOrDefault(objectId);
                var name = preset != null ? $"{preset.Name} {objectCounter}" : 
                    $"Object {objectCounter}";
                
                if (preset == null)
                {
                    Debug.LogWarning($"Preset not found for object '{objectId}'. Skipping initialization.");
                    continue;
                }

                // First we spawn object on all the states it's visible on
                var statesWithObject = SpawnObjectOnAllStates(objectId, name);

                if (!NeedsInitialization(objectId))
                    continue;
                
                // Then we initialize object on each state separately
                foreach (var stateId in statesWithObject)
                {
                    _spaceDescriptor.SetCurrentState(stateId);
                    InitializeObject(objectId);
                }
            }

            InitializeGroups();

            foreach (var toRemove in _spaceDescriptor.gameObject.GetComponentsInChildren<RemoveAfterImportBehaviour>(true))
                Object.DestroyImmediate(toRemove);
            
            // This is needed to enable correct objects for current active scene in descriptor
            _spaceDescriptor.ActiveState = _spaceDescriptor.ActiveState;
        }

        private void SpawnViewpoints()
        {
            var spaceRoot = _spaceDescriptor.transform;

            var viewpointsRoot = new GameObject("Viewpoints");
            viewpointsRoot.transform.SetParent(spaceRoot);
            viewpointsRoot.transform.ResetLocalTransform();

            var spaceId = Ti.Common.Modules.Spaces.Constants.Entities.SpaceEntityGuid;
            var spaceEntity = _spaceDescriptor.Space.GetEntity<SpaceEntity>(spaceId)!;
            var viewpointIds = spaceEntity.Get<Guid[]>(Properties.Space.VIEWPOINT_ORDER_GUID_ARR);

            if (viewpointIds == null || viewpointIds.Length == 0)
            {
                Debug.LogWarning("Failed to find any viewpoints in space!");
                return;
            }

            for (int i = 0; i < viewpointIds.Length; i++)
            {
                var viewpointId = viewpointIds[i];
                var gameObject = Object.Instantiate(ImportResources.EmptyObjectPrefab, viewpointsRoot.transform);
                var transformInfo = _spaceDescriptor.GetObjectProperty<TransformInfo>(viewpointId, Properties.TRANSFORM);
                
                gameObject.transform.localPosition = transformInfo.LocalPosition;
                gameObject.transform.localRotation = transformInfo.LocalRotation;
                gameObject.transform.localScale = transformInfo.LocalScale;

                _spaceDescriptor.AddGameObjectForEntity(viewpointId, null, gameObject, $"Viewpoint {i + 1}");
                _specialObjectIds.Add(viewpointId);
            }
        }


        private void SpawnStates()
        {
            if (_stateIds.Count == 0)
            {
                Debug.LogWarning("Failed to find any states in non-background scene!");
                return;
            }

            for (var i = 0; i < _stateIds.Count; i++)
            {
                var stateId = _stateIds[i];
                _spaceDescriptor.StateObjects.Add(SpawnFrameGameObject(stateId, $"Frame {i + 1}"));
            }
            
            SpawnFrameGameObject(BackgroundStateId, "Background");
            return;

            GameObject SpawnFrameGameObject(Guid stateId, string name)
            {
                var gameObject = Object.Instantiate(ImportResources.EmptyObjectPrefab, _spaceDescriptor.transform);
                gameObject.transform.ResetLocalTransform();
                _spaceDescriptor.AddGameObjectForEntity(stateId, null, gameObject, name);
                _specialObjectIds.Add(stateId);
                return gameObject;
            }
        }
        
        private List<Guid> SpawnObjectOnAllStates(Guid objectId, string name)
        {
            var statesWithVisibleObject = GetStatesWithVisibleObject(objectId);
            foreach (var stateId in statesWithVisibleObject)
            {
                var stateGameObject = _spaceDescriptor.GetGameObjectsForStates(stateId).First().Value;
                var gameObject = Object.Instantiate(ImportResources.EmptyObjectPrefab, stateGameObject.transform);
                var transformInfo = _spaceDescriptor.GetSnapshotProperty<TransformInfo>(objectId, stateId, Properties.TRANSFORM);
                gameObject.transform.localPosition = transformInfo.LocalPosition;
                gameObject.transform.localRotation = transformInfo.LocalRotation;
                gameObject.transform.localScale = transformInfo.LocalScale;
                _spaceDescriptor.AddGameObjectForEntity(objectId, stateId, gameObject, name);
            }

            return statesWithVisibleObject;
        }

        
        private List<Guid> GetStatesWithVisibleObject(Guid objectId)
        {
            _tempStatesList.Clear();
            var obj = _spaceDescriptor.GetObject(objectId)!;
            _tempStatesList.AddRange(
                _stateIds.Where(guid =>
                    {
                        var snapshotForState = obj.GetSnapshotForState(guid);
                        return snapshotForState is { IsActive: true };
                    }
                )
            );
            
            // Maybe the object is on background state
            if (_tempStatesList.Count == 0)
            {
                var backgroundSnap = obj.GetSnapshotForState(BackgroundStateId);
                if (backgroundSnap is { IsActive: true })
                    _tempStatesList.Add(BackgroundStateId);
            }

            return _tempStatesList;
        }

        private void InitializeObject(Guid objectId)
        {
            var objectContainer = _spaceDescriptor.GetGameObjectForCurrentState(objectId)!;
            var preset = _spaceDescriptor.ObjectPresets[objectId];

            GameObject instance;
             // Temp solution for icon presets
            if (preset is ImagePreset image)
            {
                instance = Object.Instantiate(ImportResources.ImageAssetContainer, objectContainer.transform);
                instance.transform.ResetLocalTransform();
                
                var texture = image.Image;
                if (texture != null) // Adjust aspect ratio to texture
                {
                    var currentScale = instance.transform.localScale;
                    instance.transform.localScale = new Vector3(
                        currentScale.x,
                        currentScale.y * texture.height / texture.width,
                        currentScale.z
                    );
                }
            }
            else if (preset is IconPreset icon)
            {
                instance = Object.Instantiate(ImportResources.IconAssetContainer, objectContainer.transform);
                var mesh = instance.GetComponentInChildren<ProceduralMeshBase>();
                ((SVGGenerator)mesh.Generator).SetSprite(icon.Icon!);
                
                mesh.SetDirty();
                mesh.ForceRefresh();
                instance.transform.ResetLocalTransform();
            }
            else if (preset is ProceduralMeshPreset proceduralMeshPreset)
            {
                instance = Object.Instantiate(preset.Asset, objectContainer.transform);
                var mesh = instance.GetComponentInChildren<IProceduralMesh>();
                ProceduralMeshObjectInitializer.InitializeFromPreset(mesh, proceduralMeshPreset);
                mesh.ForceRefresh();
                instance.transform.ResetLocalTransform();
            }
            else
            {
                instance = Object.Instantiate(preset!.Asset, objectContainer.transform);
                instance.transform.ResetLocalTransform();
            }
            
            instance.name = preset.name;
                
            InitializeReactors(objectId, preset, instance);
        }

        private void InitializeGroups()
        {
            var reactorsToDestroy = new HashSet<Component>();
            
            foreach (var (child, parent) in _spaceDescriptor.ObjectParents)
            {
                var parentObject = _spaceDescriptor.GetGameObjectsForStates(parent).FirstOrDefault().Value;
                if (parentObject == null)
                    continue;
                
                var reactor = parentObject.transform.GetComponentInChildren<GroupPropertyReactor>(true);
                if (reactor == null)
                    continue;
                
                var groupPreset = _spaceDescriptor.ObjectPresets[parent]!;
                var statesWithVisibleGroup = GetStatesWithVisibleObject(parent);

                foreach (var state in statesWithVisibleGroup)
                {
                    _spaceDescriptor.SetCurrentState(state);
                    var initializer = _initializerFactory.GetInitializer(reactor, groupPreset);
                    initializer?.Initialize(_spaceDescriptor, child, reactor.gameObject); 
                    reactorsToDestroy.Add(reactor);
                }

                _groups.Remove(parent);
            }

            // Destroy reactor gameobjects because objects are parented under space object
            foreach (var reactor in reactorsToDestroy)
                Object.DestroyImmediate(reactor.gameObject);

            // destroy empty groups
            foreach (var groupId in _groups)
            {
                var go = _spaceDescriptor.GetGameObjectsForStates(groupId).FirstOrDefault().Value;
                if (go)
                    Object.DestroyImmediate(go);
            }
        }

        private void InitializeReactors(Guid objectId, BasePreset preset, GameObject createdObject)
        {
            var reactors = createdObject.transform.parent.GetComponentsInChildren<PropertyReactorComponent>(true);

            IInitializer? initializer;
            SpaceMaterialReactor materialReactor = null!;

            foreach (var reactor in reactors)
            {
                if (reactor is SpaceMaterialReactor mr)
                {
                    materialReactor = mr;
                    continue;
                }

                // Groups are initialized separately
                if (reactor is GroupPropertyReactor)
                {
                    _groups.Add(objectId);
                    continue;
                }

                initializer = _initializerFactory.GetInitializer(reactor, preset);
                initializer?.Initialize(_spaceDescriptor, objectId, reactor.gameObject);
            }

            if (materialReactor == null)
            {
                return;
            }

            initializer = _initializerFactory.GetInitializer(materialReactor, preset);
            initializer?.Initialize(_spaceDescriptor, objectId, materialReactor.gameObject);
        }

        private bool NeedsInitialization(Guid objectId) => _spaceDescriptor.ObjectPresets[objectId] is not ScenePreset;

        private bool NeedsSpawning(Guid objectId)
        {
            var preset = _spaceDescriptor.ObjectPresets[objectId];
            return preset != null && preset is not EnvironmentSettingsPreset;
        }
    }
}