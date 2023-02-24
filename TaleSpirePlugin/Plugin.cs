using BepInEx;
using BepInEx.Configuration;
using Bounce.Unmanaged;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(LordAshes.FileAccessPlugin.Guid)]
    public partial class CharacterViewPlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "Character View Plug-In";              
        public const string Guid = "org.lordashes.plugins.characterview";
        public const string Version = "3.1.0.0";

        public static bool characterViewActive = false;
        public static bool characterViewLookAroundModeActive = false;

        // Configuration
        private Dictionary<string, ConfigEntry<KeyboardShortcut>> triggerKeys { get; set; } = new Dictionary<string, ConfigEntry<KeyboardShortcut>>();
        private ConfigEntry<float> rotateHeadSpeed { get; set; }
        private ConfigEntry<bool> resetHeadRotation { get; set; }
        private static float fieldOfView { get; set; }
        private static float nearClipPlane { get; set; }
        private static float defaultHeadRotation = 0.0f;
        private static float defaultHeadTilt = 0.0f;
        private static float headRotation = 0.0f;
        private static float headTilt = 0.0f;
        private static float maxHeadRotation = 45f;
        private static float headRotationSpeedScaling = 0.75f;

        private GUIStyle centeredStatus1 = new GUIStyle();
        private GUIStyle centeredStatus2 = new GUIStyle();

        void Awake()
        {
            UnityEngine.Debug.Log("Character View Plugin: "+this.GetType().AssemblyQualifiedName+" Active.");

            triggerKeys.Add("Toggle", Config.Bind("Hotkeys", "Toggle View", new KeyboardShortcut(KeyCode.V, KeyCode.RightShift)));
            triggerKeys.Add("Look", Config.Bind("Hotkeys", "Look Around Mode", new KeyboardShortcut(KeyCode.L, KeyCode.RightShift)));

            triggerKeys.Add("Head Up", Config.Bind("Hotkeys", "Rotate Head Up", new KeyboardShortcut(KeyCode.Y, KeyCode.RightShift)));
            triggerKeys.Add("Head Down", Config.Bind("Hotkeys", "Rotate Head Down", new KeyboardShortcut(KeyCode.H, KeyCode.RightShift)));
            triggerKeys.Add("Head Left", Config.Bind("Hotkeys", "Rotate Head Left", new KeyboardShortcut(KeyCode.G, KeyCode.RightShift)));
            triggerKeys.Add("Head Right", Config.Bind("Hotkeys", "Rotate Head Right", new KeyboardShortcut(KeyCode.J, KeyCode.RightShift)));

            triggerKeys.Add("Move Forward", Config.Bind("Hotkeys", "Move Forward", new KeyboardShortcut(KeyCode.Y, KeyCode.RightControl)));
            triggerKeys.Add("Move Backward", Config.Bind("Hotkeys", "Move Backward", new KeyboardShortcut(KeyCode.H, KeyCode.RightControl)));
            triggerKeys.Add("Turn Left", Config.Bind("Hotkeys", "Turn Left", new KeyboardShortcut(KeyCode.G, KeyCode.RightControl)));
            triggerKeys.Add("Turn Right", Config.Bind("Hotkeys", "Turn Right", new KeyboardShortcut(KeyCode.J, KeyCode.RightControl)));
            triggerKeys.Add("Move Left", Config.Bind("Hotkeys", "Slide Left", new KeyboardShortcut(KeyCode.B, KeyCode.RightControl)));
            triggerKeys.Add("Move Right", Config.Bind("Hotkeys", "Slide Right", new KeyboardShortcut(KeyCode.N, KeyCode.RightControl)));

            fieldOfView = Config.Bind("Settings", "Field Of View Angle", 60.0f).Value;
            maxHeadRotation = Config.Bind("Settings", "Max Head Rotation From Centre", 45f).Value;
            rotateHeadSpeed = Config.Bind("Settings", "Keyboard Rotate Head Angle", 5.0f);

            resetHeadRotation = Config.Bind("Settings", "Reset Head Rotation", true);
            defaultHeadRotation = Config.Bind("Settings", "Default Head Rotation", 0f).Value;
            defaultHeadTilt = Config.Bind("Settings", "Default Head Tilt", 0f).Value;

            nearClipPlane = Config.Bind("Settings", "Start Render At Distance", 0.5f).Value;
            headRotationSpeedScaling = Config.Bind("Settings", "Head Rotation Scaling Factor Over Distance", 1.0f).Value;

            centeredStatus1.alignment = TextAnchor.MiddleCenter;
            centeredStatus1.fontSize = 16;
            centeredStatus1.normal.textColor = Color.black;
            centeredStatus2.alignment = TextAnchor.MiddleCenter;
            centeredStatus2.fontSize = 16;
            centeredStatus2.normal.textColor = Color.white;

            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            RadialUI.RadialUIPlugin.AddCustomButtonOnCharacter(Guid+".ViewMode", new MapMenu.ItemArgs()
            {
                Action = (mm,obj) => { SelectRadialAsset(); ToggleChracterViewMode(); },
                CloseMenuOnActivate = true,
                FadeName = true,
                Icon = FileAccessPlugin.Image.LoadSprite("CharacterViewMode.png"),
                Title = "Character View",
            }, RadialMenuCheck);

            RadialUI.RadialUIPlugin.AddCustomButtonOnCharacter(Guid+".LookMode", new MapMenu.ItemArgs()
            {
                Action = (mm, obj) => { SelectRadialAsset(); ToggleChracterViewMode(); ToggleLookAroundMode(); },
                CloseMenuOnActivate = true,
                FadeName = true,
                Icon = FileAccessPlugin.Image.LoadSprite("LookAroundMode.png"),
                Title = "Look Around View",
            }, RadialMenuCheck);

            Utility.PostOnMainPage(this.GetType());
        }

        void Update()
        {
            if (Utility.StrictKeyCheck(triggerKeys["Toggle"].Value))
            {
                // Toggle Character View
                ToggleChracterViewMode();
            }
            if (Utility.StrictKeyCheck(triggerKeys["Look"].Value))
            {
                // Toggle Look Around Mode
                // Note: Intentionally not "else if" to allow both to be activated with same key binding
                ToggleLookAroundMode();
            }

            if (characterViewActive)
            {

                if (Utility.StrictKeyCheck(triggerKeys["Head Left"].Value))
                {
                    // Rotate Head Left
                    headRotation = headRotation - rotateHeadSpeed.Value;
                }
                else if (Utility.StrictKeyCheck(triggerKeys["Head Right"].Value))
                {
                    // Rotate Head Right
                    headRotation = headRotation + rotateHeadSpeed.Value;
                }
                else if (Utility.StrictKeyCheck(triggerKeys["Head Up"].Value))
                {
                    // Rotate Head Up
                    headTilt = headTilt - rotateHeadSpeed.Value;
                }
                else if (Utility.StrictKeyCheck(triggerKeys["Head Down"].Value))
                {
                    // Rotate Head Down
                    headTilt = headTilt + rotateHeadSpeed.Value;
                }
                else
                {
                    try
                    {
                        CreatureBoardAsset asset = null;
                        CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                        if (asset != null)
                        {
                            Transform baseTrans = Utility.GetBaseLoader(asset.CreatureId).transform;
                            Transform assetTrans = Utility.GetAssetLoader(asset.CreatureId).transform;
                            if (Utility.StrictKeyCheck(triggerKeys["Turn Left"].Value))
                            {
                                // Turn Asset Left
                                asset.Rotator.eulerAngles = new Vector3(asset.Rotator.eulerAngles.x, asset.Rotator.eulerAngles.y - 90, asset.Rotator.eulerAngles.z);
                            }
                            else if (Utility.StrictKeyCheck(triggerKeys["Turn Right"].Value))
                            {
                                // Turn Asset Right
                                asset.Rotator.eulerAngles = new Vector3(asset.Rotator.eulerAngles.x, asset.Rotator.eulerAngles.y + 90, asset.Rotator.eulerAngles.z);
                            }
                            else if (Utility.StrictKeyCheck(triggerKeys["Move Forward"].Value))
                            {
                                // Move Forward In Facing Direction
                                Vector3 offset = Quaternion.Euler(0, baseTrans.eulerAngles.y + 90, 0) * new Vector3(0, 0, 1);
                                asset.MoveTo(baseTrans.position - offset);
                            }
                            else if (Utility.StrictKeyCheck(triggerKeys["Move Backward"].Value))
                            {
                                // Move Backward To Facing Direction
                                Vector3 offset = Quaternion.Euler(0, baseTrans.eulerAngles.y + 90, 0) * new Vector3(0, 0, 1);
                                asset.MoveTo(baseTrans.position + offset);
                            }
                            else if (Utility.StrictKeyCheck(triggerKeys["Move Left"].Value))
                            {
                                // Slide Left With Respect To Facing Direction
                                Vector3 offset = Quaternion.Euler(0, baseTrans.eulerAngles.y - 90 + 90, 0) * new Vector3(0, 0, 1);
                                asset.MoveTo(baseTrans.position - offset);
                            }
                            else if (Utility.StrictKeyCheck(triggerKeys["Move Right"].Value))
                            {
                                // Slide Right With Respect To Facing Direction
                                Vector3 offset = Quaternion.Euler(0, baseTrans.eulerAngles.y + 90 + 90, 0) * new Vector3(0, 0, 1);
                                asset.MoveTo(baseTrans.position - offset);
                            }
                        }
                        else
                        {
                            SystemMessage.DisplayInfoText("No Selected Character.\r\nEnsure A Mini Was Selected\r\nWhen Using This Feature.");
                            characterViewActive = false;
                            characterViewLookAroundModeActive = false;
                        }
                    }
                    catch
                    {
                        SystemMessage.DisplayInfoText("Character View Mode Failed.\r\nEnsure A Mini Was Selected\r\nWhen Using This Feature.");
                        characterViewActive = false;
                        characterViewLookAroundModeActive = false;
                    }
                }

                if (characterViewLookAroundModeActive)
                {
                    // Adjust Head Rotation And Tilt Base On Mouse
                    Vector3 headOrientation = GetHeadOrientation(Input.mousePosition);
                    headRotation = headOrientation.x;
                    headTilt = headOrientation.y;
                }
            }
        }

        void OnGUI()
        {
            if (characterViewActive)
            {
                // Provide head status
                GUI.Label(new Rect(1, 61, Screen.width, 15*(1080/Screen.height)), "Character View Mode: Head Rotation = " + headRotation.ToString("0.0") + ", Head Tilt = " + headTilt.ToString("0.0"), centeredStatus1);
                GUI.Label(new Rect(0, 60, Screen.width, 15*(1080/Screen.height)), "Character View Mode: Head Rotation = " + headRotation.ToString("0.0") + ", Head Tilt = " + headTilt.ToString("0.0"), centeredStatus2);
            }
        }

        private void SelectRadialAsset()
        {
            foreach(CreatureBoardAsset asset in CreaturePresenter.AllCreatureAssets)
            {
                asset.Deselect();
            }
            CreatureBoardAsset radialAsset = null;
            CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out radialAsset);
            if(radialAsset!=null)
            {
                radialAsset.Select();
                LocalClient.SelectedCreatureId = radialAsset.CreatureId;
            }
        }

        private void ToggleChracterViewMode()
        {
            // Toggle Character View
            characterViewActive = !characterViewActive;
            characterViewLookAroundModeActive = (characterViewLookAroundModeActive && characterViewActive);
            SetPostProcessing(!characterViewActive);
            if (resetHeadRotation.Value == true)
            {
                headRotation = defaultHeadRotation;
                headTilt = defaultHeadTilt;
            }
            UnityEngine.Debug.Log("Character View Plugin: Character View Mode Swicth To "+characterViewActive);
            UnityEngine.Debug.Log("Character View Plugin: Character Look Around Mode Swicth To " + characterViewLookAroundModeActive);
        }

        private void ToggleLookAroundMode()
        {
            // Toggle Look Around Mode
            characterViewLookAroundModeActive = (!characterViewLookAroundModeActive && characterViewActive);
            UnityEngine.Debug.Log("Character View Plugin: Character View Mode Swicth To " + characterViewActive);
            UnityEngine.Debug.Log("Character View Plugin: Character Look Around Mode Swicth To " + characterViewLookAroundModeActive);
        }

        private bool RadialMenuCheck(NGuid arg1, NGuid arg2)
        {
            CreatureBoardAsset asset;
            CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out asset);
            if(asset!=null)
            {
                return CreatureManager.PlayerCanControlCreature(LocalPlayer.Id, asset.CreatureId);
            }
            return false;
        }

        private Vector3 GetHeadOrientation(Vector3 screenPosition)
        {
            // Convert mouse pointer position to use coordinates with 0,0 at the center of the screen
            screenPosition.x = screenPosition.x - (Screen.width / 2);
            screenPosition.y = -1*(screenPosition.y - (Screen.height / 2));
            // Determine the max scaled width and max scaled height 
            Vector3 max = new Vector3();
            max.x = (float)Math.Pow((double)(Screen.width/2),(double)headRotationSpeedScaling);
            max.y = (float)Math.Pow((double)(Screen.height/2), (double)headRotationSpeedScaling);
            // Determine head orientation (rotation and tilt)
            Vector3 scaling = new Vector3();
            // Restore the direction after scaling
            scaling.x = (float)(Math.Sign(screenPosition.x) * Math.Pow(Math.Abs((double)screenPosition.x), (double)headRotationSpeedScaling));
            scaling.y = (float)(Math.Sign(screenPosition.y) * Math.Pow(Math.Abs((double)screenPosition.y), (double)headRotationSpeedScaling));
            scaling.x = scaling.x / max.x;
            scaling.y = scaling.y / max.y;
            // Determine rotation and tilt based on distance from center 
            Vector3 orientation = new Vector3();
            orientation.x = maxHeadRotation * scaling.x;
            orientation.y = maxHeadRotation * scaling.y;
            orientation.z = 0;
            // Bound rotation and tilt angles
            if (orientation.x < (-1 * maxHeadRotation)) { orientation.x = (-1 * maxHeadRotation); }
            if (orientation.x > maxHeadRotation) { orientation.x = maxHeadRotation; }
            if (orientation.y < (-1 * maxHeadRotation)) { orientation.y = (-1 * maxHeadRotation); }
            if (orientation.y > maxHeadRotation) { orientation.y = maxHeadRotation; }
            // Debug.Log("Character View Plugin: "+screenPosition.x+" -> "+scaling.x+" -> "+maxHeadRotation+"x"+scaling.x+" = "+orientation.x+ ", " + screenPosition.y + " -> " + scaling.y + " -> " + maxHeadRotation + "x" + scaling.y + " = " + orientation.y);
            return orientation;
        }

        private void SetPostProcessing(bool setting)
        {
            var postProcessLayer = Camera.main.GetComponent<PostProcessLayer>();
            postProcessLayer.enabled = setting;
        }
    }
}
