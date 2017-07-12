﻿/*
 * Copyright(c) Live2D Inc. All rights reserved.
 * 
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at http://live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */


using System;
using System.Collections.Generic;
using System.Linq;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Rendering;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;


namespace Live2D.Cubism.Editor.Importers
{
    /// <summary>
    /// Handles importing of Cubism models.
    /// </summary>
    [Serializable]
    public sealed class CubismModel3JsonImporter : CubismImporterBase
    {
        /// <summary>
        /// <see cref="Model3Json"/> backing field.
        /// </summary>
        [NonSerialized] private CubismModel3Json _model3Json;

        /// <summary>
        ///<see cref="CubismModel3Json"/> asset.
        /// </summary>
        public CubismModel3Json Model3Json
        {
            get
            {
                if (_model3Json == null)
                {
                    _model3Json = CubismModel3Json.LoadAtPath(AssetPath);
                }


                return _model3Json;
            }
        }


        /// <summary>
        /// Guid of model prefab.
        /// </summary>
        [SerializeField] private string _modelPrefabGuid;

        /// <summary>
        /// <see cref="ModelPrefab"/> backing field.
        /// </summary>
        [NonSerialized] private GameObject _modelPrefab;

        /// <summary>
        /// Prefab of model.
        /// </summary>
        private GameObject ModelPrefab
        {
            get
            {
                if (_modelPrefab == null)
                {
                    _modelPrefab = AssetGuid.LoadAsset<GameObject>(_modelPrefabGuid);
                }


                return _modelPrefab;
            }
            set
            {
                _modelPrefab = value;
                _modelPrefabGuid = AssetGuid.GetGuid(value);
            }
        }


        /// <summary>
        /// Guid of moc.
        /// </summary>
        [SerializeField]
        private string _mocAssetGuid;

        /// <summary>
        /// <see cref="MocAsset"/> backing field.
        /// </summary>
        [NonSerialized]
        private CubismMoc _mocAsset;

        /// <summary>
        /// Moc asset.
        /// </summary>
        private CubismMoc MocAsset
        {
            get
            {
                if (_mocAsset == null)
                {
                    _mocAsset = AssetGuid.LoadAsset<CubismMoc>(_mocAssetGuid);
                }


                return _mocAsset;
            }
            set
            {
                _mocAsset = value;
                _mocAssetGuid = AssetGuid.GetGuid(value);
            }
        }

#region Unity Event Handling

        /// <summary>
        /// Registers importer.
        /// </summary>
        [InitializeOnLoadMethod]
        // ReSharper disable once UnusedMember.Local
        private static void RegisterImporter()
        {
            CubismImporter.RegisterImporter<CubismModel3JsonImporter>(".model3.json");
        }

#endregion

#region CubismImporterBase

        /// <summary>
        /// Imports the corresponding asset.
        /// </summary>
        public override void Import()
        {
            var isImporterDirty = false;


            // Instantiate model source and model.
            var model = Model3Json.ToModel(CubismImporter.OnPickMaterial, CubismImporter.OnPickTexture);
            var moc = model.Moc;


            // Create moc asset.
            if (MocAsset == null)
            {
                AssetDatabase.CreateAsset(moc, AssetPath.Replace(".model3.json", ".asset"));


                MocAsset = moc;


                isImporterDirty = true;
            }

            // Update moc asset.
            else
            {
                EditorUtility.CopySerialized(moc, MocAsset);
                EditorUtility.SetDirty(MocAsset);
            }


            // Create model prefab.
            if (ModelPrefab == null)
            {
                // Trigger event.
                CubismImporter.SendModelImportEvent(this, model);


                foreach (var texture in Model3Json.Textures)
                {
                    CubismImporter.SendModelTextureImportEvent(this, model, texture);
                }


                // Create prefab and trigger saving of changes.
                ModelPrefab = PrefabUtility.CreatePrefab(AssetPath.Replace(".model3.json", ".prefab"), model.gameObject);


                isImporterDirty = true;
            }


            // Update model prefab.
            else
            {
                // Copy all user data over from previous model.
                var source = Object.Instantiate(ModelPrefab).FindCubismModel();


                CopyUserData(source, model);
                Object.DestroyImmediate(source.gameObject, true);
                

                // Trigger events.
                CubismImporter.SendModelImportEvent(this, model);


                foreach (var texture in Model3Json.Textures)
                {
                    CubismImporter.SendModelTextureImportEvent(this, model, texture);
                }


                // Replace prefab.
                EditorUtility.CopySerialized(model.gameObject, ModelPrefab);
                EditorUtility.SetDirty(ModelPrefab);


                // Log event.
                CubismImporter.LogReimport(AssetPath, AssetDatabase.GUIDToAssetPath(_modelPrefabGuid));
            }


            // Clean up.
            Object.DestroyImmediate(model.gameObject, true);


            // Save state and assets.
            if (isImporterDirty)
            {
                Save();
            }
            else
            {
                AssetDatabase.SaveAssets();
            }
        }

#endregion

        private static void CopyUserData(CubismModel source, CubismModel destination)
        {
            // Give parameters, parts, and drawables special treatment.
            CopyUserData(source.Parameters, destination.Parameters);
            CopyUserData(source.Parts, destination.Parts);
            CopyUserData(source.Drawables, destination.Drawables);


            // Copy children.
            foreach (var child in source.transform
                .GetComponentsInChildren<Transform>()
                .Where(t => t != source.transform)
                .Select(t => t.gameObject))
            {
                // Skip parameters, parts, and drawables.
                if (child.name == "Parameters")
                {
                    continue;
                }

                if (child.name == "Parts")
                {
                    continue;
                }

                if (child.name == "Drawables")
                {
                    continue;
                }


                Object.Instantiate(child, destination.transform);
            }


            // Copy components.
            foreach (var sourceComponent in source.GetComponents(typeof(Component)))
            {
                // Skip non-movable components.
                if (!sourceComponent.MoveOnCubismReimport())
                {
                    continue;
                }


                // Copy component.
                var destinationComponent = destination.GetOrAddComponent(sourceComponent.GetType());


                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }
        }


        private static void CopyUserData<T>(T[] source, T[] destination) where T : MonoBehaviour
        {
            foreach (var destinationT in destination)
            {
                var sourceT = source.FirstOrDefault(p => p.name == destinationT.name);


                // Skip removed parameters.
                if (sourceT == null)
                {
                    continue;
                }


                // Copy any children.
                foreach (var child in sourceT.transform
                    .GetComponentsInChildren<Transform>()
                    .Where(t => t != sourceT.transform)
                    .Select(t => t.gameObject))
                {
                    Object.Instantiate(child, destinationT.transform);
                }


                // Copy components.
                foreach (var sourceComponent in sourceT.GetComponents(typeof(Component)))
                {
                    // Skip non-movable components.
                    if (!sourceComponent.MoveOnCubismReimport())
                    {
                        continue;
                    }


                    // Copy component.
                    var destinationComponent = destinationT.GetOrAddComponent(sourceComponent.GetType());


                    EditorUtility.CopySerialized(sourceComponent, destinationComponent);
                }
            }
        }
    }
}