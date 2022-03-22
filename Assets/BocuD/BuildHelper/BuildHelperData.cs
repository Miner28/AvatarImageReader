/* MIT License
 Copyright (c) 2021 BocuD (github.com/BocuD)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using static BocuD.VRChatApiTools.VRChatApiTools;
namespace BocuD.BuildHelper
{
    [ExecuteInEditMode]
    public class BuildHelperData : MonoBehaviour
    {
        public string sceneID;
        public OverrideContainer[] overrideContainers;
        public BuildHelperUdon linkedBehaviour;
        public GameObject linkedBehaviourGameObject;

        public BranchStorageObject dataObject;
        
        private void Awake()
        {
#if UNITY_EDITOR
            if (linkedBehaviourGameObject != null)
                linkedBehaviour = linkedBehaviourGameObject.GetUdonSharpComponent<BuildHelperUdon>();
#endif
        }

        public static void RunLastBuildChecks()
        {
            foreach (Branch b in GetDataObject().branches)
            {
                foreach (PlatformBuildInfo info in b.buildData.PlatformBuildInfos())
                {
                    bool exists = File.Exists(info.buildPath);
                    info.buildValid = exists && ComputeFileMD5(info.buildPath) == info.buildHash;
                }
            }
        }

        private void Reset()
        {
            sceneID = GetUniqueID();
            
            dataObject = new BranchStorageObject
            {
                branches = new Branch[0],
            };

            overrideContainers = new OverrideContainer[0];
        }

        private static BuildHelperData dataBehaviour;
        
        public static BuildHelperData GetDataBehaviour()
        {
            if (dataBehaviour) return dataBehaviour;
            
            dataBehaviour = FindObjectOfType<BuildHelperData>();
            return dataBehaviour;
        }

        public static BranchStorageObject GetDataObject()
        {
            BuildHelperData data = GetDataBehaviour();
            return data ? data.dataObject : null;
        }

        public void PrepareExcludedGameObjects()
        {
            for (int i = 0; i < dataObject.branches.Length; i++)
            {
                if (!dataObject.branches[i].hasOverrides) continue;
            
                OverrideContainer container = overrideContainers[i];
                foreach (GameObject obj in container.ExclusiveGameObjects)
                {
                    obj.tag = "EditorOnly";
                    obj.SetActive(false);
                }
            }
        }

        public void SaveToJSON()
        {
            if (dataObject.branches == null) dataObject.branches = new Branch[0];

            string savePath = GetSavePath(sceneID);
            CheckIfFileExists(savePath);

            string json = JsonUtility.ToJson(dataObject, true);
            File.WriteAllText(savePath, json);
        }

        public void LoadFromJSON()
        {
            string savePath = GetSavePath(sceneID);
            CheckIfFileExists(savePath);
            
            string json = File.ReadAllText(savePath);
            dataObject = JsonUtility.FromJson<BranchStorageObject>(json);
        }

        private static void CheckIfFileExists(string savePath)
        {
            // Create file
            if (!File.Exists(savePath))
            {
                if (!Directory.Exists(Application.dataPath + "/Resources/BuildHelper/"))
                {
                    Directory.CreateDirectory(Application.dataPath + "/Resources/BuildHelper/");
                }

                StreamWriter sw = File.CreateText(savePath);
                
                //write something so it won't be empty on next load
                BranchStorageObject storageObject = new BranchStorageObject();

                sw.Write(JsonUtility.ToJson(storageObject));

                if (storageObject.branches == null) storageObject.branches = new Branch[0];

                sw.Close();
            }
        }
        
        public void DeleteJSON()
        {
            string savePath = GetSavePath(sceneID);
        
            // Create file
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }

        private static string GetSavePath(string sceneID)
        {
            //legacy data save
            if (sceneID == "")
            {
                return Application.dataPath + $"/Resources/BuildHelper/{SceneManager.GetActiveScene().name}.json";
            }
            
            //GUID based save
            return Application.dataPath + $"/Resources/BuildHelper/{sceneID}.json";
        }
        
        public static string GetUniqueID()
        {
            string [] split = DateTime.Now.TimeOfDay.ToString().Split(new Char [] {':','.'});
            string id = "";
            for (int i = 0; i < split.Length; i++)
            {
                id += split[i];
            }

            id = long.Parse(id).ToString("X");
            
            return id;
        }
        
        public async Task OnSuccesfulPublish(Branch b, BlueprintInfo blueprintInfo, DateTime uploadTime, int uploadVersion = -1)
        {
            Branch target = dataObject.branches.First(br => br.branchID == b.branchID);
            
            ApiWorld world = await FetchApiWorldAsync(blueprintInfo.blueprintID);
            
            target.editedName = world.name;
            target.editedDescription = world.name;
            target.editedCap = world.capacity;
            target.editedTags = world.publicTags.ToList();

            target.nameChanged = false;
            target.descriptionChanged = false;
            target.capacityChanged = false;
            target.tagsChanged = false;

            target.vrcImageHasChanges = false;
            target.vrcImageWarning = "";
            
            target.buildData.CurrentPlatformBuildData().UploadTime = uploadTime;
            target.buildData.CurrentPlatformBuildData().uploadVersion = uploadVersion == -1 ? target.buildData.CurrentPlatformBuildData().buildVersion : uploadVersion;
            target.buildData.justUploaded = true;

            if (target.blueprintID == null || target.blueprintID != blueprintInfo.blueprintID)
                target.blueprintID = blueprintInfo.blueprintID;

            if (target.vrcImageHasChanges)
            {
                AssetDatabase.DeleteAsset(target.overrideImagePath);
                target.vrcImageHasChanges = false;
                target.vrcImageWarning = "";
            }
            
            ClearCaches();
        }
    }

    public static class ApiToolsExtensions
    {
        public static WorldInfo ToWorldInfo(this Branch branch)
        {
            return new WorldInfo()
            {
                name = branch.nameChanged ? branch.editedName : branch.cachedName,
                description = branch.descriptionChanged ? branch.editedDescription : branch.cachedDescription,
                tags = branch.tagsChanged ? branch.editedTags.ToList() : branch.cachedTags.ToList(),
                capacity = branch.capacityChanged ? branch.editedCap : branch.cachedCap,
                
                blueprintID = branch.blueprintID,
                
                newImagePath = branch.vrcImageHasChanges ? branch.overrideImagePath : ""
            };
        }
    }

    [Serializable]
    public class BranchStorageObject
    {
        public int currentBranch;

        public Branch[] branches;
        public Branch CurrentBranch
        {
            get
            {
                if (currentBranch >= 0 && currentBranch < branches.Length)
                    return branches[currentBranch];
                
                return null;
            }
        }
    }

    [Serializable]
    public class Branch
    {
        //basic branch information
        public string name = "";
        public bool hasOverrides = false;
        public string blueprintID = "";
        public string branchID = "";
        public bool remoteExists = false;

        //VRC World Data overrides
        public string cachedName = "Unpublished VRChat world";
        public string cachedDescription = "";
        public int cachedCap = 16;
        public string cachedRelease = "New world";
        public List<string> cachedTags = new List<string>();

        public string editedName = "New VRChat World";
        public string editedDescription = "Fancy description for your world";
        public int editedCap = 16;
        public List<string> editedTags = new List<string>();
        
        public bool nameChanged = false;
        public bool descriptionChanged = false;
        public bool capacityChanged = false;
        public bool tagsChanged = false;
        public bool vrcImageHasChanges = false;
        public string overrideImagePath = "";
        public string vrcImageWarning = "";
    
        //VRCCam state
        public bool saveCamPos = true;
        public bool uniqueCamPos = false;
        public Vector3 camPos = Vector3.zero;
        public Quaternion camRot = Quaternion.identity;
    
        public BuildData buildData;

        public bool hasDeploymentData = false;
        public DeploymentData deploymentData;

        public bool hasUdonLink = false;

        public bool HasVRCDataChanges()
        {
            return nameChanged || descriptionChanged || capacityChanged || tagsChanged || vrcImageHasChanges;
        }
    }

    [Serializable]
    public class BuildData
    {
        public bool justUploaded;
        public PlatformBuildInfo pcData;
        public PlatformBuildInfo androidData;
        
        public void SaveBuildTime()
        {
            CurrentPlatformBuildData().BuildTime = DateTime.Now;
        }

        public void SaveUploadTime()
        {
            CurrentPlatformBuildData().UploadTime = DateTime.Now;
        }

        public PlatformBuildInfo GetLatestBuild()
        {
            return pcData.buildVersion > androidData.buildVersion ? pcData : androidData;
        }

        public PlatformBuildInfo GetLatestUpload()
        {
            return pcData.uploadVersion > androidData.uploadVersion ? pcData : androidData;
        }

        public PlatformBuildInfo CurrentPlatformBuildData()
        {
            return CurrentPlatform() == Platform.Windows ? pcData : androidData;
        }

        public IEnumerable<PlatformBuildInfo> PlatformBuildInfos()
        {
            return new [] { pcData, androidData };
        }

        public BuildData()
        {
            pcData = new PlatformBuildInfo(Platform.Windows);
            androidData = new PlatformBuildInfo(Platform.Android);
        }
    }

    [Serializable]
    public class PlatformBuildInfo
    {
        //yes, these should be DateTime for sure.. they are strings because DateTime was not being serialised correctly somehow.. i hate it.
        public string buildPath;
        public string buildHash;
        public string buildTime;
        public int buildVersion;
        public bool buildValid = false;

        public string uploadTime;
        public int uploadVersion;

        public Platform platform;

        public DateTime BuildTime
        {
            set => buildTime = value.ToString(CultureInfo.InvariantCulture);
            get => DateTime.Parse(buildTime, CultureInfo.InvariantCulture);
        }

        public DateTime UploadTime
        {
            set => uploadTime = value.ToString(CultureInfo.InvariantCulture);
            get => DateTime.Parse(uploadTime, CultureInfo.InvariantCulture);
        }

        public PlatformBuildInfo(Platform platform)
        {
            buildTime = "";
            buildVersion = -1;
            uploadTime = "";
            uploadVersion = -1;
            this.platform = platform;
        }
    }

    [Serializable]
    public class OverrideContainer
    {
        public GameObject[] ExclusiveGameObjects;
        public GameObject[] ExcludedGameObjects;

        public OverrideContainer()
        {
            ExclusiveGameObjects = new GameObject[0];
            ExcludedGameObjects = new GameObject[0];
        }
        
        public void ApplyStateChanges()
        {
            foreach (GameObject obj in ExclusiveGameObjects)
            {
                EnableGameObject(obj);
            }

            foreach (GameObject obj in ExcludedGameObjects)
            {
                DisableGameObject(obj);
            }
        }

        public void ResetStateChanges()
        {
            foreach (GameObject obj in ExcludedGameObjects)
            {
                EnableGameObject(obj);
            }
        }

        public static void EnableGameObject(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                if (obj.CompareTag("EditorOnly"))
                {
                    obj.tag = "Untagged";
                }
            }
        }
    
        public static void DisableGameObject(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                if (!obj.CompareTag("EditorOnly"))
                {
                    obj.tag = "EditorOnly";
                }
            }
        }
    }
}

#endif