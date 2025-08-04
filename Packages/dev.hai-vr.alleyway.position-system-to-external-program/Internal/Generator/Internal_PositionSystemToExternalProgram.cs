#if IS_RESILIENCE_DEV
using System;
using System.Collections.Generic;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using Object = UnityEngine.Object;

namespace Internal.Generator
{
    public class Internal_PositionSystemToExternalProgram : MonoBehaviour
    {
        public Animator systemRoot;
        public RuntimeAnimatorController controller;
        public AnimationClip anim_disabled;
        public AnimationClip anim_local;
        
        public AnimationClip anim_visibility_hidden;
        public AnimationClip anim_visibility_visible;
        
        public AnimationClip anim_bringToHand_freeze;
        public AnimationClip anim_bringToHand_bring;
        
        public Motion dbt_direct;
        public Motion bt_visible0;
        public Motion bt_visible1;
        public Motion bt_bringToHand;

        public Texture2D icon_blank;

        public VRCExpressionParameters parameters;
        public VRCExpressionsMenu menu;

        public GameObject go_localOnly;
        public GameObject go_squareRescale;
        public GameObject go_calibrationMesh;
        public VRCPositionConstraint constraint_calibration_position;
        public VRCAimConstraint constraint_calibration_aim;
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(Internal_PositionSystemToExternalProgram))]
    public class Internal_PositionSystemToExternalProgramEditor : Editor
    {
        private const string Param_Enabled = "PStoEP_Enabled";
        private const string Param_EnabledAndVisible = "PStoEP_EnabledAndVisible";
        private const string Param_BringToHand = "PStoEP_BringToHand";
        
        private AacFlBase aac;
        private Internal_Rewiring rewiring;

        public override void OnInspectorGUI()
        {
            var my = (Internal_PositionSystemToExternalProgram)target;

            DrawDefaultInspector();

            if (GUILayout.Button("(DEVELOPER ONLY) Generate"))
            {
                Generate(my);
            }

            if (GUILayout.Button("(DEVELOPER ONLY) Create a new blend tree asset"))
            {
                var bt = new BlendTree();
                AssetDatabase.CreateAsset(bt, $"Packages/dev.hai-vr.alleyway.position-system-to-external-program/Internal/Animator/{Guid.NewGuid().ToString().Substring(0, 9)}.asset");
                EditorGUIUtility.PingObject(bt);
            }
        }

        private void Generate(Internal_PositionSystemToExternalProgram my)
        {
            aac = AacV1.Create(new AacConfiguration
            {
                SystemName = "PositionSystemToExternalProgram",
                AnimatorRoot = my.systemRoot.transform,
                AssetContainer = null,
                ContainerMode = AacConfiguration.Container.Never,
                AssetKey = "1234",
                DefaultsProvider = new AacDefaultsProvider(writeDefaults: true),
                AssetContainerProvider = null,
            });
            rewiring = new Internal_Rewiring(aac);
            
            rewiring.ClearAnimatorController(my.controller);
            
            var ctrl = rewiring.EditAnimatorController(my.controller);
            {
                rewiring.ResetClip(my.anim_disabled)
                    .Toggling(my.go_localOnly, false)
                    .Scaling(my.go_squareRescale, Vector3.one * 0.001f);
            
                rewiring.ResetClip(my.anim_local)
                    .Toggling(my.go_localOnly, true)
                    .Scaling(my.go_squareRescale, Vector3.one);
                
                var layer = ctrl.NewLayer("Locality");

                var disabled = layer.NewState("Init").WithAnimation(my.anim_disabled);
                var enabled = layer.NewState("Enabled-Local").WithAnimation(my.anim_local);
                var enabledAndVisible = layer.NewState("EnabledAndVisible-Local").WithAnimation(my.anim_local).RightOf(enabled);
                var toEnabledTRANSITION = layer.NewState("Enabled-LocalTransition").WithAnimation(my.anim_local).Under(enabled);
                var toEnabledAndVisibleTRANSITION = layer.NewState("EnabledAndVisible-LocalTransition").WithAnimation(my.anim_local).Under(enabledAndVisible);

                var enabledParam = layer.FloatParameter(Param_Enabled);
                var enabledAndVisibleParam = layer.FloatParameter(Param_EnabledAndVisible);

                toEnabledTRANSITION.Driving(driver => driver.Sets(enabledAndVisibleParam, 0f).Locally());
                toEnabledAndVisibleTRANSITION.Driving(driver => driver.Sets(enabledParam, 0f).Locally());

                disabled.TransitionsTo(enabled)
                    .When(layer.Av3().ItIsLocal()).And(enabledParam.IsGreaterThan(0.5f));
                disabled.TransitionsTo(enabledAndVisible)
                    .When(layer.Av3().ItIsLocal()).And(enabledAndVisibleParam.IsGreaterThan(0.5f));
                
                enabled.TransitionsTo(disabled)
                    .When(layer.Av3().ItIsRemote()).Or()
                    .When(enabledParam.IsLessThan(0.5f));
                
                enabledAndVisible.TransitionsTo(disabled)
                    .When(layer.Av3().ItIsRemote()).Or()
                    .When(enabledAndVisibleParam.IsLessThan(0.5f));

                enabled.TransitionsTo(toEnabledAndVisibleTRANSITION).When(enabledAndVisibleParam.IsGreaterThan(0.5f));
                enabledAndVisible.TransitionsTo(toEnabledTRANSITION).When(enabledParam.IsGreaterThan(0.5f));

                toEnabledAndVisibleTRANSITION.AutomaticallyMovesTo(enabledAndVisible);
                toEnabledTRANSITION.AutomaticallyMovesTo(enabled);
            }
            {
                var layer = ctrl.NewLayer("DBT");

                var one = layer.FloatParameter("PStoEP_One");
                layer.OverrideValue(one, 1f);
                
                var enabledParam = layer.FloatParameter(Param_Enabled);
                var enabledAndVisibleParam = layer.FloatParameter(Param_EnabledAndVisible);
                var bringToHandParam = layer.FloatParameter(Param_BringToHand);

                // It needs to be visible only when `enabledAndVisible OR (enabled AND bringToHand)`

                rewiring.ResetClip(my.anim_visibility_hidden)
                    .Toggling(my.go_calibrationMesh, false);
                rewiring.ResetClip(my.anim_visibility_visible)
                    .Toggling(my.go_calibrationMesh, true);
                
                var btVisible1 = rewiring.ResetBlendTree(my.bt_visible1 as BlendTree)
                    .FreeformCartesian2D(enabledParam, bringToHandParam)
                    .WithAnimation(my.anim_visibility_hidden, new Vector2(0, 0))
                    .WithAnimation(my.anim_visibility_hidden, new Vector2(0, 1))
                    .WithAnimation(my.anim_visibility_hidden, new Vector2(1, 0))
                    .WithAnimation(my.anim_visibility_visible, new Vector2(1, 1));
                
                var btVisible0 = rewiring.ResetBlendTree(my.bt_visible0 as BlendTree)
                    .Simple1D(enabledAndVisibleParam)
                    .WithAnimation(btVisible1, 0)
                    .WithAnimation(my.anim_visibility_visible, 1);

                rewiring.ResetClip(my.anim_bringToHand_freeze)
                    .Animating(clip =>
                    {
                        // In the object hierarchy, the weight is 0 in order to keep it at the origin. It IS normal to set the weight to 1 on both branches
                        clip.Animates(my.constraint_calibration_position, "GlobalWeight").WithOneFrame(1f);
                        clip.Animates(my.constraint_calibration_position, "FreezeToWorld").WithOneFrame(1f);
                        clip.Animates(my.constraint_calibration_aim, "GlobalWeight").WithOneFrame(1f);
                        clip.Animates(my.constraint_calibration_aim, "FreezeToWorld").WithOneFrame(1f);
                    });
                rewiring.ResetClip(my.anim_bringToHand_bring)
                    .Animating(clip =>
                    {
                        // In the object hierarchy, the weight is 0 in order to keep it at the origin. It IS normal to set the weight to 1 on both branches
                        clip.Animates(my.constraint_calibration_position, "GlobalWeight").WithOneFrame(1f);
                        clip.Animates(my.constraint_calibration_position, "FreezeToWorld").WithOneFrame(0f);
                        clip.Animates(my.constraint_calibration_aim, "GlobalWeight").WithOneFrame(1f);
                        clip.Animates(my.constraint_calibration_aim, "FreezeToWorld").WithOneFrame(0f);
                    });

                var btBringToHand = rewiring.ResetBlendTree(my.bt_bringToHand as BlendTree)
                    .Simple1D(bringToHandParam)
                    .WithAnimation(my.anim_bringToHand_freeze, 0f)
                    .WithAnimation(my.anim_bringToHand_bring, 1f);
                
                var dbt = rewiring.ResetBlendTree(my.dbt_direct as BlendTree)
                    .Direct()
                    .WithAnimation(btVisible0, one)
                    .WithAnimation(btBringToHand, one);

                var dbtState = layer.NewState("DBT");
                dbtState.WithAnimation(dbt);
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(my.controller));
            Debug.Log($"{assets.Length} items");
            foreach (var o in assets)
            {
                Debug.Log($"{o} {o.GetType()}");
            }

            my.parameters.parameters = new VRCExpressionParameters.Parameter[]
            {
                new()
                {
                    name = Param_Enabled,
                    networkSynced = false,
                    saved = false,
                    valueType = VRCExpressionParameters.ValueType.Bool
                },
                new()
                {
                    name = Param_EnabledAndVisible,
                    networkSynced = true,
                    saved = false,
                    valueType = VRCExpressionParameters.ValueType.Bool
                },
                new()
                {
                    name = Param_BringToHand,
                    networkSynced = true,
                    saved = false,
                    valueType = VRCExpressionParameters.ValueType.Bool
                }
            };

            my.menu.controls = new List<VRCExpressionsMenu.Control>
            {
                new()
                {
                    name = "Enabled",
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = Param_Enabled },
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                },
                new()
                {
                    name = "Enabled and Visible",
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = Param_EnabledAndVisible },
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                },
                new()
                {
                    name = " ",
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
                    type = VRCExpressionsMenu.Control.ControlType.Button,
                    icon = my.icon_blank,
                },
                new()
                {
                    name = "Calibrate to Hand",
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = Param_BringToHand },
                    type = VRCExpressionsMenu.Control.ControlType.Button,
                },
            };
            my.menu.Parameters = my.parameters;
            
            EditorUtility.SetDirty(my.parameters);
            EditorUtility.SetDirty(my.menu);

            rewiring.SetDirtyAll();
        }
    }

    internal class Internal_Rewiring
    {
        private readonly AacFlBase _aac;
        private readonly HashSet<Object> _objects = new();

        internal Internal_Rewiring(AacFlBase aac)
        {
            _aac = aac;
        }

        public void SetDirtyAll()
        {
            foreach (var obj in _objects)
            {
                EditorUtility.SetDirty(obj);
            }
        }

        public AacFlClip ResetClip(AnimationClip editClip)
        {
            editClip.ClearCurves();
            return EditClip(editClip);
        }

        public AacFlClip EditClip(AnimationClip editClip)
        {
            _objects.Add(editClip);
            
            var aacClip = _aac.NewClip();
            typeof(AacFlClip)
                .GetField("<Clip>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(aacClip, editClip);

            return aacClip;
        }

        public AacFlController EditAnimatorController(RuntimeAnimatorController controller)
        {
            _objects.Add(controller);
            
            var aacController = _aac.NewAnimatorController();
            typeof(AacFlController).GetField("<AnimatorController>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(aacController, controller);

            return aacController;
        }

        public void ClearAnimatorController(RuntimeAnimatorController controller)
        {
            if (controller is not AnimatorController animController) return;

            _objects.Add(animController);
            
            while (animController.layers.Length > 0)
            {
                animController.RemoveLayer(0);
            }
            
            var parameters = animController.parameters;
            for (var i = parameters.Length - 1; i >= 0; i--)
            {
                animController.RemoveParameter(i);
            }
        }

        public AacFlNonInitializedBlendTree ResetBlendTree(BlendTree blendTree)
        {
            _objects.Add(blendTree);

            blendTree.children = new ChildMotion[0];

            var aacBlendTree = _aac.NewBlendTree();
            typeof(AacFlBlendTree).GetField("<BlendTree>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(aacBlendTree, blendTree);

            return aacBlendTree;
        }
    }
#endif
}
#endif