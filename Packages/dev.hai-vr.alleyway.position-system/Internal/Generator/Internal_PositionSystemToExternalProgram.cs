#if IS_RESILIENCE_DEV
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using UnityEditor;
using UnityEditor.Animations;
#endif

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
        public Texture2D icon_mainMenuFolder;
        public Texture2D icon_enabled;
        public Texture2D icon_enabledAndVisible;
        public Texture2D icon_bringToHand;

        public VRCExpressionParameters parameters;
        public VRCExpressionsMenu menu;
        public VRCExpressionsMenu subMenu;

        public GameObject go_localOnly;
        public GameObject go_squareRescale;
        public GameObject go_calibrationMesh;
        public VRCPositionConstraint constraint_calibration_position;
        public VRCAimConstraint constraint_calibration_aim;
        
        // ChilloutVR
        public bool isChilloutVR;
        public PositionConstraint CVR__constraint_calibration_position;
        public AimConstraint CVR__constraint_calibration_aim;
        public ParentConstraint CVR__constraint_system_freezeToWorld;
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(Internal_PositionSystemToExternalProgram))]
    public class Internal_PositionSystemToExternalProgramEditor : Editor
    {
        private const string Param_Enabled = "PStoEP_Enabled";
        private const string Param_EnabledAndVisible = "PStoEP_EnabledAndVisible";
        private const string Param_BringToHand = "PStoEP_BringToHand";
        
        private AacFlBase aac;
        private AacFlModification rewiring;

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
            // ReSharper disable once InconsistentNaming
            var isVRC = !my.isChilloutVR;
            
            aac = AacV1.Create(new AacConfiguration
            {
                SystemName = isVRC ? "PositionSystemToExternalProgram" : "ChilloutVR-AbsolutePaths-PositionSystemToExternalProgram",
                AnimatorRoot = my.systemRoot.transform,
                AssetContainer = null,
                ContainerMode = AacConfiguration.Container.Never,
                AssetKey = "1234",
                DefaultsProvider = new AacDefaultsProvider(writeDefaults: true),
                AssetContainerProvider = null,
            });
            rewiring = aac.Modification();
            
            rewiring.ClearAnimatorController(my.controller as AnimatorController);
            
            var ctrl = rewiring.EditAnimatorController(my.controller as AnimatorController);
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

                if (isVRC)
                {
                    toEnabledTRANSITION.Driving(driver => driver.Sets(enabledAndVisibleParam, 0f).Locally());
                    toEnabledAndVisibleTRANSITION.Driving(driver => driver.Sets(enabledParam, 0f).Locally());
                }

                // VRC and CVR both have IsLocal:bool, so we don't need to change this.
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
                        if (isVRC)
                        {
                            clip.Animates(my.constraint_calibration_position, "GlobalWeight").WithOneFrame(1f);
                            clip.Animates(my.constraint_calibration_position, "FreezeToWorld").WithOneFrame(1f);
                            clip.Animates(my.constraint_calibration_aim, "GlobalWeight").WithOneFrame(1f);
                            clip.Animates(my.constraint_calibration_aim, "FreezeToWorld").WithOneFrame(1f);
                        }
                        else if (my.isChilloutVR)
                        {
                            clip.Animates(my.CVR__constraint_system_freezeToWorld, "m_Weight").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_position, "m_Weight").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_position, "m_Enabled").WithOneFrame(0f);
                            clip.Animates(my.CVR__constraint_calibration_aim, "m_Weight").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_aim, "m_Enabled").WithOneFrame(0f);
                        }
                    });
                rewiring.ResetClip(my.anim_bringToHand_bring)
                    .Animating(clip =>
                    {
                        // In the object hierarchy, the weight is 0 in order to keep it at the origin. It IS normal to set the weight to 1 on both branches
                        if (isVRC)
                        {
                            clip.Animates(my.constraint_calibration_position, "GlobalWeight").WithOneFrame(1f);
                            clip.Animates(my.constraint_calibration_position, "FreezeToWorld").WithOneFrame(0f);
                            clip.Animates(my.constraint_calibration_aim, "GlobalWeight").WithOneFrame(1f);
                            clip.Animates(my.constraint_calibration_aim, "FreezeToWorld").WithOneFrame(0f);
                        }
                        else if (my.isChilloutVR)
                        {
                            clip.Animates(my.CVR__constraint_system_freezeToWorld, "m_Weight").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_position, "m_Weight").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_position, "m_Enabled").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_aim, "m_Weight").WithOneFrame(1f);
                            clip.Animates(my.CVR__constraint_calibration_aim, "m_Enabled").WithOneFrame(1f);
                        }
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

            if (isVRC)
            {
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
                        name = "Position System",
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = my.subMenu,
                        icon = my.icon_mainMenuFolder
                    }
                };
                my.menu.Parameters = my.parameters;
                
                my.subMenu.controls = new List<VRCExpressionsMenu.Control>
                {
                    new()
                    {
                        name = "Enabled",
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = Param_Enabled },
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        icon = my.icon_enabled,
                    },
                    new()
                    {
                        name = "Enabled and Visible",
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = Param_EnabledAndVisible },
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        icon = my.icon_enabledAndVisible,
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
                        icon = my.icon_bringToHand,
                    },
                };
                my.subMenu.Parameters = my.parameters;
                
                EditorUtility.SetDirty(my.parameters);
                EditorUtility.SetDirty(my.menu);
                EditorUtility.SetDirty(my.subMenu);
            }

            rewiring.SetDirtyAll();
        }
    }
#endif
}
#endif