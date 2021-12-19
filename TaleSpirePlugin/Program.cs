using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using BepInEx;
using Bounce.Unmanaged;
using System.Linq;
using BepInEx.Configuration;
using System.Collections.Generic;
using System;

namespace LordAshes
{
    [BepInPlugin(Guid, "Character View Plug-In", Version)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    public partial class CharacterViewPlugin : BaseUnityPlugin
    {
        // Plugin info
        private const string Guid = "org.lordashes.plugins.characterview";
        private const string Version = "1.4.1.0";

        // Plugin Directory
        private string data = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase.ToString().Replace("file:///", ""));

        // Stores if the current view is in Character View mode or Regular View mode
        private bool characterView = false;

        // Stores the last camera position and rotation before activating Character View
        // (used to restore the camera view when exiting Character View mode)
        private Vector3 defaultCameraPos;
        private Vector3 defaultCameraRot;

        // Stores if the post processing functionality was enabled or not before activating Character View
        // (used to restore the post processing setting when exiting Character View mode)
        private bool usePP = true;

        // Used to turn diagnostics on or off
        private bool diagnostic = true;

        // Determines how much in front of the mini the camera is placed.
        // Lower values have chances that part of the mini may block the view. 
        // Higher values have a chance to miss content between the mini and the view.
        private const float cameraForwardOffset = 0.5f;

        // Determines how high the camera is offset when in Character View.
        // Lower values allows tiles directly in front of the character to be seen but are not realistic depiction of the character's view.
        // Higher values are a more realistic depiction of what the character sees but will not show content near the floor that is immeidately infront of the mini.
        private const float cameraHeightOffset = 0.5f;

        // Configuration
        private ConfigEntry<bool> usePostProcessing { get; set; }
        private ConfigEntry<float> tiltDefault { get; set; }
        private ConfigEntry<bool> resetHead { get; set; }
        private static float tilt { get; set; }
        private static float swivel { get; set; }
        
        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>
        void Awake()
        {
            UnityEngine.Debug.Log("CharacterViewPlugin: Select character and press ? to toggle between Character View and Regular View modes.");

            // Post plugin on TaleSpire main page
            Utility.PostOnMainPage(this.GetType());

            // Load configuration
            usePostProcessing = Config.Bind("Settings", "Use Post Processing When In CharacterView", false);
            tiltDefault = Config.Bind("Settings", "Tilt Angle", 10f);
            tilt = tiltDefault.Value;
            swivel = 0;
            resetHead = Config.Bind("Settings", "Reset Head Angle Between Views", true);

            // Add Character View to mini Radial menu
            Texture2D tex = new Texture2D(32, 32);
            tex.LoadImage(System.IO.File.ReadAllBytes(data + "/CustomData/Images/Icons/CharacterView.png"));
            Sprite icon = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            RadialUI.RadialUIPlugin.AddCustomButtonOnCharacter(CharacterViewPlugin.Guid, new MapMenu.ItemArgs
            {
                Action = (mmi, obj) =>
                {
                    ToggleView(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature())); 
                },
                Icon = icon,
                Title = "Character View",
                CloseMenuOnActivate = true
            }, Reporter);

        }

        /// <summary>
        /// Method to track which asset has the radial menu open
        /// </summary>
        /// <param name="selected"></param>
        /// <param name="radialMenu"></param>
        /// <returns></returns>
        private bool Reporter(NGuid selected, NGuid radialMenu)
        {
            return LocalClient.HasControlOfCreature(new CreatureGuid(radialMenu));
        }

        /// <summary>
        /// Function for determining if view mode has been toggled and, if so, activating or deactivating Character View mode.
        /// This function is called periodically by TaleSpire.
        /// </summary>
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.U) && characterView)
            {
                Debug.Log("Character View: Tilt +");
                tilt = tilt + 5;
                if (tilt > 175) { tilt = 175; }
                ToggleView(LocalClient.SelectedCreatureId, true);
            }
            else if (Input.GetKeyDown(KeyCode.J) && characterView)
            {
                Debug.Log("Character View: Tilt -");
                tilt = tilt - 5;
                if (tilt < -175) { tilt = -175; }
                ToggleView(LocalClient.SelectedCreatureId, true);
            }
            else if (Input.GetKeyDown(KeyCode.H) && characterView)
            {
                Debug.Log("Character View: Swivel -");
                swivel = swivel -5;
                if (swivel < -45) { swivel = -45; }
                ToggleView(LocalClient.SelectedCreatureId, true);
            }
            else if (Input.GetKeyDown(KeyCode.K) && characterView)
            {
                Debug.Log("Character View: Swivel +");
                swivel = swivel + 5;
                if (swivel > 45) { swivel = 45; }
                ToggleView(LocalClient.SelectedCreatureId, true);
            }
            else if (Input.GetKeyDown(KeyCode.N) && characterView)
            {
                Debug.Log("Character View: Recenter");
                tilt = tiltDefault.Value;
                swivel = 0;
                ToggleView(LocalClient.SelectedCreatureId, true);
            }
            // Character View fucntionality is triggered by the ? or / key
            else if (Input.GetKeyDown(KeyCode.Question) || Input.GetKeyDown(KeyCode.Slash) || (Input.anyKeyDown && characterView))
            {
                Debug.Log("Character View: View Toggle");
                // Ensure that there is a camera controller instance
                if (CameraController.HasInstance)
                {
                    // Ensure that there is a board session manager instance
                    if (BoardSessionManager.HasInstance)
                    {
                        // Ensure that there is a board
                        if (BoardSessionManager.HasBoardAndIsInNominalState)
                        {
                            // Ensure that the board is not loading
                            if (!BoardSessionManager.IsLoading)
                            {
                                ToggleView(LocalClient.SelectedCreatureId);
                            }
                        }
                    }
                }
            }

        }

        void OnGUI()
        {
            if(characterView)
            {
                GUIStyle gs = new GUIStyle()
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16
                };
                gs.normal.textColor = Color.white;
                GUI.Label(new Rect(0,60,1920,30),"Head: Tilt "+tilt+", Swivel "+swivel, gs);
            }
        }

        public void ToggleView(CreatureGuid target, bool update = false)
        {
            Debug.Log("Character View: Toggle / Update");
            // Get reference to the camera
            Camera camera = CameraController.GetCamera();

            // If Character View mode is not active then activate it
            Debug.Log("Character View: Character View = "+characterView+" / Update = "+update);
            if (!characterView || update)
            {
                // Switch the mode indicator (Character View mode)
                characterView = true;

                if (!update)
                {
                    // Get post processing status
                    usePP = GetPostProcessing();
                    // Turn off post processing
                    SetPostProcessing(usePostProcessing.Value);
                    // Save camera position and rotation before character view
                    defaultCameraPos = camera.transform.position;
                    defaultCameraRot = camera.gameObject.transform.eulerAngles;

                    if (diagnostic) { UnityEngine.Debug.Log("Saving Camera: " + defaultCameraPos.x + "," + defaultCameraPos.y + "," + defaultCameraPos.z + " facing " + defaultCameraRot.y); }
                }

                // Find the selected character
                CreatureBoardAsset asset;
                CreaturePresenter.TryGetAsset(target,out asset);
                if (asset != null)
                {
                    // Process creature's view
                    if (!update) { SystemMessage.DisplayInfoText("Viewing As " + asset.Creature.Name + "..."); }

                    // Get the selected character's current position and rotation

                    Vector3 pos = asset.HookHead.position;
                    Vector3 rot = asset.Rotator.eulerAngles;

                    if (diagnostic) { UnityEngine.Debug.Log(asset.Creature.Name + " is at " + pos.x + "," + pos.y + "," + pos.z + " facing " + rot.y+" / Head Tilt "+tilt+" Swivel "+swivel); }

                    // Locate spot just in front of mini to avoid seeing through mini
                    Quaternion rotation = Quaternion.AngleAxis((rot.y - 90) + swivel, Vector3.up);
                    Vector3 magnitude = new Vector3(0, 0, cameraForwardOffset);
                    Vector3 dir = rotation * magnitude;

                    if (diagnostic) { UnityEngine.Debug.Log("Camera offset is " + dir.x + "," + dir.y + "," + dir.z); }

                    // Apply the mini offset to the mini position to determine the camera position
                    pos.x += dir.x;
                    pos.y = asset.HookHead.position.y;
                    pos.z += dir.y;

                    // Apply camera position and rotation
                    camera.gameObject.transform.position = pos;
                    camera.gameObject.transform.eulerAngles = new Vector3(tilt, (rot.y - 90) + swivel, 0);
                }
            }
            else
            {
                // Switch the mode indicator (Regular View mode)
                characterView = false;

                if (diagnostic) { UnityEngine.Debug.Log("Restoring Camera: " + defaultCameraPos.x + "," + defaultCameraPos.y + "," + defaultCameraPos.z + " facing " + defaultCameraRot.y); }

                // Restore post processing to its previous state
                SetPostProcessing(usePP);

                // Restore camera position and rotation to its pre-character view state
                camera.gameObject.transform.position = defaultCameraPos;
                camera.gameObject.transform.eulerAngles = defaultCameraRot;

                SystemMessage.DisplayInfoText("Regular View Restored...");

                if (resetHead.Value) { tilt = tiltDefault.Value; swivel = 0; }
            }
            if (diagnostic) { UnityEngine.Debug.Log("Camera is at " + camera.transform.position.x + "," + camera.transform.position.y + "," + camera.transform.position.z + " facing " + camera.transform.eulerAngles.y); }

        }

        /// <summary>
        /// Function for getting the post processing enabled status
        /// </summary>
        /// <returns>Returns a boolean indicating if post processing is enabled</returns>
        private bool GetPostProcessing()
        {
            var postProcessLayer = Camera.main.GetComponent<PostProcessLayer>();
            return postProcessLayer.enabled;
        }

        /// <summary>
        /// Function for setting the post processing enabled setting
        /// </summary>
        /// <param name="setting">Boolean indicating if post processing is enabled or not</param>
        private void SetPostProcessing(bool setting)
        {
            var postProcessLayer = Camera.main.GetComponent<PostProcessLayer>();
            postProcessLayer.enabled = setting;
        }
    }
}
