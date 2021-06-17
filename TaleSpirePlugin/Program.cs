using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using BepInEx;
using Bounce.Unmanaged;
using System.Linq;
using BepInEx.Configuration;

namespace LordAshes
{
    [BepInPlugin(Guid, "Character View Plug-In", Version)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    public class CharacterViewPlugin : BaseUnityPlugin
    {
        // Plugin info
        private const string Guid = "org.lordashes.plugins.characterview";
        private const string Version = "1.2.0.0";

        // Content directory
        private string dir = UnityEngine.Application.dataPath.Substring(0, UnityEngine.Application.dataPath.LastIndexOf("/")) + "/TaleSpire_CustomData/";

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

        // Creature whose radial menu is open
        private CreatureGuid radialCreature = CreatureGuid.Empty;

        // Configuration
        private ConfigEntry<bool> usePostProcessing { get; set; }
        
        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>
        void Awake()
        {
            UnityEngine.Debug.Log("CharacterViewPlugin: Select character and press ? to toggle between Character View and Regular View modes.");

            // Post plugin on TaleSpire main page
            StateDetection.Initialize(this.GetType());

            // Load configuration
            usePostProcessing = Config.Bind("Settings", "Use Post Processing When In CharacterView", true);

            // Add Character View to mini Radial menu
            Texture2D tex = new Texture2D(32, 32);
            tex.LoadImage(System.IO.File.ReadAllBytes(dir + "Images/Icons/CharacterView.Png"));
            Sprite icon = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            RadialUI.RadialUIPlugin.AddOnCharacter(CharacterViewPlugin.Guid, new MapMenu.ItemArgs
            {
                Action = (mmi, obj) =>
                {
                    ToggleView(radialCreature); 
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
            radialCreature = new CreatureGuid(radialMenu);
            return true;
        }

        /// <summary>
        /// Function for determining if view mode has been toggled and, if so, activating or deactivating Character View mode.
        /// This function is called periodically by TaleSpire.
        /// </summary>
        void Update()
        {
            // Character View fucntionality is triggered by the ? or / key
            if (Input.GetKeyDown(KeyCode.Question) || Input.GetKeyDown(KeyCode.Slash) || (Input.anyKeyDown && characterView))
            {
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

        public void ToggleView(CreatureGuid target)
        {
            // Get reference to the camera
            Camera camera = CameraController.GetCamera();

            // If Character View mode is not active then activate it
            if (!characterView)
            {
                // Switch the mode indicator (Character View mode)
                characterView = true;

                // Get post processing status
                usePP = GetPostProcessing();
                // Turn off post processing
                SetPostProcessing(usePostProcessing.Value);
                // Save camera position and rotation before character view
                defaultCameraPos = camera.transform.position;
                defaultCameraRot = camera.gameObject.transform.eulerAngles;

                if (diagnostic) { UnityEngine.Debug.Log("Saving Camera: " + defaultCameraPos.x + "," + defaultCameraPos.y + "," + defaultCameraPos.z + " facing " + defaultCameraRot.y); }

                // Find the selected character
                CreatureBoardAsset asset;
                CreaturePresenter.TryGetAsset(target,out asset);
                if (asset != null)
                {
                    // Process creature's view
                    SystemMessage.DisplayInfoText("Viewing As " + asset.Creature.Name + "...");

                    // Get the selected character's current position and rotation

                    Vector3 pos = // asset.CorrectPos;                          // <- Doesn't update on move (Load position?)
                                  // asset.PlacedPosition;                      // <- Updated when mini is not held
                                  // asset.LastDropPosition;                    // <- Updated when mini is not held
                                  // asset.Creature.LastPlacedPosition;         // <- Updated when mini is not held
                                     asset.gameObject.transform.position;       // or asset.Creature.gameObject.transform.position;

                    Vector3 rot = asset.Rotator.eulerAngles;

                    if (diagnostic) { UnityEngine.Debug.Log(asset.Creature.Name + " is at " + pos.x + "," + pos.y + "," + pos.z + " facing " + rot.y); }

                    // Locate spot just in front of mini to avoid seeing through mini
                    Quaternion rotation = Quaternion.AngleAxis((rot.y - 90), Vector3.up);
                    Vector3 magnitude = new Vector3(0, 0, cameraForwardOffset);
                    Vector3 dir = rotation * magnitude;

                    if (diagnostic) { UnityEngine.Debug.Log("Camera offset is " + dir.x + "," + dir.y + "," + dir.z); }

                    // Apply the mini offset to the mini position to determine the camera position
                    pos.x += dir.x;
                    pos.y += cameraHeightOffset;
                    pos.x += dir.y;

                    // Apply camera position and rotation
                    camera.gameObject.transform.position = pos;
                    camera.gameObject.transform.eulerAngles = new Vector3(0, (rot.y - 90), 0);
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
