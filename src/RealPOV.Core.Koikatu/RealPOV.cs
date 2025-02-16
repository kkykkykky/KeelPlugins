using BepInEx;
using HarmonyLib;
using RealPOV.Core;
using Studio;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using KKAPI.Studio.SaveLoad;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: System.Reflection.AssemblyFileVersion(RealPOV.Koikatu.RealPOV.Version)]

namespace RealPOV.Koikatu
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class RealPOV : RealPOVCore
    {
        public const string Version = "1.3.0." + BuildNumber.Version;

        private ConfigEntry<bool> HideHead { get; set; }

        private static int backupLayer;
        private static ChaControl currentChara;
        private static Queue<ChaControl> charaQueue;
        private readonly bool isStudio = Paths.ProcessName == "CharaStudio";
        private bool prevVisibleHeadAlways;
        private HFlag hFlag;
        private static int currentCharaId = -1;
        private static RealPOV plugin;

        private float dofOrigSize;
        private float dofOrigAperature;

        protected override void Awake()
        {
            plugin = this;
            defaultFov = 45f;
            defaultViewOffset = 0.05f;
            base.Awake();

            HideHead = Config.Bind(SECTION_GENERAL, "Hide character head", false, "Whene entering POV, hide the character's head. Prevents accessories and hair from obstructing the view.");

            Harmony.CreateAndPatchAll(GetType());
            StudioSaveLoadApi.RegisterExtraBehaviour<SceneDataController>(GUID);

            SceneManager.sceneLoaded += (arg0, scene) =>
            {
                hFlag = FindObjectOfType<HFlag>();
                charaQueue = null;
            };
            SceneManager.sceneUnloaded += arg0 => charaQueue = null;
        }

        public static void EnablePov(ScenePovData povData)
        {
            if(Studio.Studio.Instance.dicObjectCtrl.TryGetValue(povData.CharaId, out var chara))
            {
                currentChara = ((OCIChar)chara).charInfo;
                currentCharaId = chara.objectInfo.dicKey;
                currentCharaGo = currentChara.gameObject;
                LookRotation[currentCharaGo] = povData.Rotation;
                CurrentFOV = povData.Fov;
                plugin.EnablePov();
            }
        }

        public static ScenePovData GetPovData()
        {
            if(currentCharaId == -1)
                return null;
            
            return new ScenePovData
            {
                CharaId = currentCharaId,
                Fov = CurrentFOV.Value,
                Rotation = LookRotation[currentCharaGo]
            };
        }

        protected override void EnablePov()
        {
            if(!currentChara)
            {
                if(isStudio)
                {
                    var selectedCharas = GuideObjectManager.Instance.selectObjectKey.Select(x => Studio.Studio.GetCtrlInfo(x) as OCIChar).Where(x => x != null).ToList();
                    if(selectedCharas.Count > 0)
                    {
                        var ociChar = selectedCharas.First();
                        currentChara = ociChar.charInfo;
                        currentCharaId = ociChar.objectInfo.dicKey;
                        currentCharaGo = currentChara.gameObject;
                    }
                    else
                    {
                        Logger.LogMessage("Select a character in workspace to enter its POV");
                    }
                }
                else
                {
                    Queue<ChaControl> CreateQueue()
                    {
                        return new Queue<ChaControl>(FindObjectsOfType<ChaControl>());
                    }
                    
                    ChaControl GetCurrentChara()
                    {
                        for(int i = 0; i < charaQueue.Count; i++)
                        {
                            var chaControl = charaQueue.Dequeue();

                            // Remove destroyed
                            if(!chaControl)
                                continue;

                            // Rotate the queue
                            charaQueue.Enqueue(chaControl);
                            if(chaControl.sex == 0 && hFlag && (hFlag.mode == HFlag.EMode.aibu || hFlag.mode == HFlag.EMode.lesbian || hFlag.mode == HFlag.EMode.masturbation)) continue;
                            // Found a valid character, otherwise skip (needed for story mode H because roam mode characters are in the queue too, just disabled)
                            if(chaControl.objTop.activeInHierarchy) return chaControl;
                        }
                        return null;
                    }

                    if(charaQueue == null)
                        charaQueue = CreateQueue();

                    currentChara = GetCurrentChara();
                    if(!currentChara)
                    {
                        charaQueue = CreateQueue();
                        currentChara = GetCurrentChara();
                    }

                    currentCharaGo = currentChara?.gameObject;
                }
            }

            if(currentChara)
            {
                prevVisibleHeadAlways = currentChara.fileStatus.visibleHeadAlways;
                if(HideHead.Value) currentChara.fileStatus.visibleHeadAlways = false;

                GameCamera = Camera.main;
                var cc = (MonoBehaviour)GameCamera.GetComponent<CameraControl_Ver2>() ?? GameCamera.GetComponent<Studio.CameraControl>();
                if(cc) cc.enabled = false;
                
                // Fix depth of field being completely out of focus
                var depthOfField = GameCamera.GetComponent<UnityStandardAssets.ImageEffects.DepthOfField>();
                dofOrigSize = depthOfField.focalSize;
                dofOrigAperature = depthOfField.aperture;
                if(depthOfField.enabled)
                {
                    depthOfField.focalTransform.localPosition = new Vector3(0, 0, 0.25f);
                    depthOfField.focalSize = 0.9f;
                    depthOfField.aperture = 0.6f;
                }

                // only use head rotation if there is no existing rotation
                if(!LookRotation.TryGetValue(currentCharaGo, out _))
                {
                    LookRotation[currentCharaGo] = currentChara.objHeadBone.transform.rotation.eulerAngles;
                }
                else
                {
                    // always get z axis from head
                    var rot = LookRotation[currentCharaGo];
                    LookRotation[currentCharaGo] = new Vector3(rot.x, rot.y, currentChara.objHeadBone.transform.rotation.eulerAngles.z);
                }

                base.EnablePov();

                backupLayer = GameCamera.gameObject.layer;
                GameCamera.gameObject.layer = 0;
            }
        }

        protected override void DisablePov()
        {
            currentChara.fileStatus.visibleHeadAlways = prevVisibleHeadAlways;
            currentChara = null;
            currentCharaId = -1;

            var cc = (MonoBehaviour)GameCamera.GetComponent<CameraControl_Ver2>() ?? GameCamera.GetComponent<Studio.CameraControl>();
            if(cc) cc.enabled = true;

            var depthOfField = GameCamera.GetComponent<UnityStandardAssets.ImageEffects.DepthOfField>();
            depthOfField.focalSize = dofOrigSize;
            depthOfField.aperture = dofOrigAperature;

            base.DisablePov();

            GameCamera.gameObject.layer = backupLayer;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(NeckLookControllerVer2), "LateUpdate")]
        private static bool ApplyRotation(NeckLookControllerVer2 __instance)
        {
            if(POVEnabled)
            {
                if(!currentChara)
                {
                    POVEnabled = false;
                    return true;
                }

                Vector3 rot;
                if(LookRotation.TryGetValue(currentCharaGo, out var val))
                    rot = val;
                else
                    LookRotation[currentCharaGo] = rot = currentChara.objHeadBone.transform.rotation.eulerAngles;

                if(__instance.neckLookScript && currentChara.neckLookCtrl == __instance)
                {
                    __instance.neckLookScript.aBones[0].neckBone.rotation = Quaternion.identity;
                    __instance.neckLookScript.aBones[1].neckBone.rotation = Quaternion.identity;
                    __instance.neckLookScript.aBones[1].neckBone.Rotate(rot);

                    var eyeObjs = currentChara.eyeLookCtrl.eyeLookScript.eyeObjs;
                    GameCamera.transform.position = Vector3.Lerp(eyeObjs[0].eyeTransform.position, eyeObjs[1].eyeTransform.position, 0.5f);
                    GameCamera.transform.rotation = currentChara.objHeadBone.transform.rotation;
                    GameCamera.transform.Translate(Vector3.forward * ViewOffset.Value);
                    GameCamera.fieldOfView = CurrentFOV.Value;

                    return false;
                }
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "ChangeAnimator")]
        [HarmonyPatch(typeof(HFlag), nameof(HFlag.selectAnimationListInfo), MethodType.Setter)]
        private static void ResetAllRotations()
        {
            LookRotation.Clear();
        }
    }
}
