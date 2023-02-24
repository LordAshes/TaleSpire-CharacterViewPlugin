using BepInEx;
using BepInEx.Configuration;
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LordAshes
{
    public partial class CharacterViewPlugin : BaseUnityPlugin
    {
        public static class Utility
        {
            public static void PostOnMainPage(System.Reflection.MemberInfo plugin)
            {
                SceneManager.sceneLoaded += (scene, mode) =>
                {
                    try
                    {
                        if (scene.name == "UI")
                        {
                            TextMeshProUGUI betaText = GetUITextByName("BETA");
                            if (betaText)
                            {
                                betaText.text = "INJECTED BUILD - unstable mods";
                            }
                        }
                        else
                        {
                            TextMeshProUGUI modListText = GetUITextByName("TextMeshPro Text");
                            if (modListText)
                            {
                                BepInPlugin bepInPlugin = (BepInPlugin)Attribute.GetCustomAttribute(plugin, typeof(BepInPlugin));
                                if (modListText.text.EndsWith("</size>"))
                                {
                                    modListText.text += "\n\nMods Currently Installed:\n";
                                }
                                modListText.text += "\nLord Ashes' " + bepInPlugin.Name + " - " + bepInPlugin.Version;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex);
                    }
                };
            }

            /// <summary>
            /// Function to check if the board is loaded
            /// </summary>
            /// <returns></returns>
            public static bool isBoardLoaded()
            {
                return CameraController.HasInstance && BoardSessionManager.HasInstance && !BoardSessionManager.IsLoading;
            }

            /// <summary>
            /// Method to properly evaluate shortcut keys. 
            /// </summary>
            /// <param name="check"></param>
            /// <returns></returns>
            public static bool StrictKeyCheck(KeyboardShortcut check)
            {
                if (!check.IsUp()) { return false; }
                foreach (KeyCode modifier in new KeyCode[] { KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift })
                {
                    if (Input.GetKey(modifier) != check.Modifiers.Contains(modifier)) { return false; }
                }
                return true;
            }

            private static TextMeshProUGUI GetUITextByName(string name)
            {
                TextMeshProUGUI[] texts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>();
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].name == name)
                    {
                        return texts[i];
                    }
                }
                return null;
            }

            /// <summary>
            /// Method to obtain the Base Loader Game Object based on a CreatureGuid
            /// </summary>
            /// <param name="cid">Creature Guid</param>
            /// <returns>BaseLoader Game Object</returns>
            public static GameObject GetBaseLoader(CreatureGuid cid)
            {
                CreatureBoardAsset asset = null;
                CreaturePresenter.TryGetAsset(cid, out asset);
                if (asset != null)
                {
                    CreatureBase _base = null;
                    StartWith<CreatureBase>(asset, "_base", ref _base);
                    Transform baseLoader = null;
                    Traverse(_base.transform, "BaseLoader", ref baseLoader);
                    if (baseLoader != null)
                    {
                        return baseLoader.GetChild(0).gameObject;
                    }
                    else
                    {
                        Debug.LogWarning("Character View Plugin: Could Not Find Base Loader");
                        return null;
                    }
                }
                return null;
            }

            /// <summary>
            /// Method to obtain the Asset Loader Game Object based on a CreatureGuid
            /// </summary>
            /// <param name="cid">Creature Guid</param>
            /// <returns>AssetLoader Game Object</returns>
            public static GameObject GetAssetLoader(CreatureGuid cid)
            {
                CreatureBoardAsset asset = null;
                CreaturePresenter.TryGetAsset(cid, out asset);
                if (asset != null)
                {
                    Transform _creatureRoot = null;
                    StartWith(asset, "_creatureRoot", ref _creatureRoot);
                    Transform assetLoader = null;
                    Traverse(_creatureRoot, "AssetLoader", ref assetLoader);
                    if (assetLoader != null)
                    {
                        return assetLoader.GetChild(0).gameObject;
                    }
                    else
                    {
                        Debug.LogWarning("Character View Plugin: Could Not Find Asset Loader");
                        return null;
                    }
                }
                return null;
            }

            public static void StartWith<T>(CreatureBoardAsset asset, string seek, ref T match)
            {
                Type type = typeof(CreatureBoardAsset);
                match = default(T);
                foreach (FieldInfo fi in type.GetRuntimeFields())
                {
                    if (fi.Name == seek) 
                    {
                        match = (T)fi.GetValue(asset); 
                        break; 
                    }
                }
            }

            public static void Traverse(Transform root, string seek, ref Transform match)
            {
                // Debug.Log("Seeking Child Named '" + seek + "'. Found '" + root.name + "'");
                if (match != null) { return; }
                if (root.name == seek) { match = root; return; }
                foreach (Transform child in root.Children())
                {
                    Traverse(child, seek, ref match);
                }
            }
        }
    }
}
