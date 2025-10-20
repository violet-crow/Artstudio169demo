using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ShapesXR.Import;
using ShapesXR.Import.Presets;
using ShapesXR.Import.Settings;
using ShapesXR.ImportCore.Entities;
using Ti.Common.Modules.Spaces.Models;
using Ti.Common.Modules.Spaces.Props;
using UnityEngine;
using Space = ShapesXR.Import.Space;

namespace ShapesXR
{
    public class SpaceDescriptor : MonoBehaviour
#if UNITY_EDITOR
    , ISpaceDescriptor
    {
        [SerializeField, HideInInspector] private List<GameObject> _stateObjects = new();
        [SerializeField, HideInInspector] private string _accessCode = null!;
        [SerializeField, HideInInspector] private string _instanceId = null!;
        [SerializeField] private int _activeState;

        private readonly Dictionary<Guid, Dictionary<Guid, GameObject>> _gameObjectsOnStates = new();

        public Space Space { get; set; } = null!;
        
        public List<GameObject> StateObjects => _stateObjects;
        public InstanceCache InstanceCache => Space.InstanceCache;
        public Dictionary<Guid, Resource> Resources => Space.Resources;
        public IEnumerable<SpaceObject> Objects => Space.GetEntities<SpaceObject>(EntityType.Object);
        public Dictionary<Guid, BasePreset?> ObjectPresets { get; } = new();
        public Dictionary<Guid, Guid> ObjectParents { get; } = new();

        public SpaceObject? GetObject(Guid entityId) => Space.GetEntity<SpaceObject>(entityId);
        
        public bool GetObject(Guid entityId, [NotNullWhen(true)] out SpaceObject? spaceObject) =>
            Space.TryGetEntity(entityId, out spaceObject);

        public Dictionary<Guid, GameObject> GetGameObjectsForStates(Guid entityId) =>
            _gameObjectsOnStates.GetValueOrDefault(entityId) ?? new Dictionary<Guid, GameObject>();
        
        public Guid CurrentState { get; private set; }

        public void AddGameObjectForEntity(Guid entityId, Guid? stateId, GameObject go, string gameObjectName)
        {
            go.name = gameObjectName;
            if (!_gameObjectsOnStates.ContainsKey(entityId))
                _gameObjectsOnStates.Add(entityId, new Dictionary<Guid, GameObject>());
            
            stateId ??= Guid.Empty;
            _gameObjectsOnStates[entityId][stateId.Value] = go;
            
            var spaceObject = GetObject(entityId);
            if (spaceObject != null)
            {
                // Need to add name to space object to correctly name materials after
                spaceObject.SetName(go.name); 
            }
        }

        public string AccessCode
        {
            get => _accessCode;
            private set
            {
                _accessCode = value;
                _instanceId = Guid.NewGuid().ToString().Substring(0, 8);
            }
        }

        public string InstanceId => _instanceId;

        public static SpaceDescriptor Create(Space space, string accessCode)
        {
            var  descriptor = Instantiate(ImportResources.SpaceDescriptorPrefab);
            descriptor.name = $"Space - {accessCode}";
            var instance = descriptor.GetComponent<SpaceDescriptor>();
            instance.Space = space;
            instance.AccessCode = accessCode;
            return instance;
        }

        public int ActiveState
        {
            get => _activeState;
            set
            {
                _activeState = value;
                
                for (var i = 0; i < StateObjects.Count; i++)
                {
                    if (StateObjects[i])
                        StateObjects[i].SetActive(_activeState == i);
                }
            }
        }

        public void SetCurrentState(Guid stateGuid) => CurrentState = stateGuid;
        
        public void ReadObjectProperties()
        {
            foreach (var obj in Objects)
            {
                var presetId = obj.Get<Guid>(Properties.PRESET_GUID);
                ImportResources.PresetLibrary.TryGetPreset(presetId, out var preset);
                
                ObjectPresets[obj.Id] = preset;

                var parentId = obj.Get<ParentInfo>(Properties.PARENT).ParentId;
                if (parentId != default)
                    ObjectParents[obj.Id] = parentId;
            }
        }
    }
#else
        {}
#endif
}