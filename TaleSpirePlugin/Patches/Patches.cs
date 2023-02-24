using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LordAshes
{
    public partial class CharacterViewPlugin : BaseUnityPlugin
    {
        [HarmonyPatch(typeof(Camera), "FireOnPreRender")]
        public static class CameraPatches
        {
            public static bool Prefix(ref Camera cam)
            {
                if (cam != null)
                {
                    if (CharacterViewPlugin.characterViewActive)
                    {
                        CreatureBoardAsset asset;
                        CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                        if (asset != null)
                        {
                            Transform assetTrans = Utility.GetBaseLoader(asset.CreatureId).transform;
                            cam.fieldOfView = fieldOfView;
                            cam.nearClipPlane = nearClipPlane;
                            cam.transform.position = asset.HookHead.position;
                            cam.transform.eulerAngles = new Vector3(headTilt, assetTrans.eulerAngles.y - 90 + headRotation, 0);
                        }
                        else
                        {
                            CharacterViewPlugin.characterViewActive = false;
                            CharacterViewPlugin.characterViewLookAroundModeActive = false;
                        }
                    }
                }
                else
                {
                    CharacterViewPlugin.characterViewActive = false;
                    CharacterViewPlugin.characterViewLookAroundModeActive = false;
                }
                return true;
            }
        }
    }
}