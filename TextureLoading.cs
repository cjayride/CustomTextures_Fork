using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomTextures {
    public partial class BepInExPlugin : BaseUnityPlugin {
        private static void LoadCustomTextures() {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CustomTextures");

            if (!Directory.Exists(path)) {
                Dbgl($"Directory {path} does not exist! Creating.");
                Directory.CreateDirectory(path);
                return;
            }


            texturesToLoad.Clear();

            foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)) {
                string fileName = Path.GetFileName(file);
                string id = Path.GetFileNameWithoutExtension(fileName);


                if (!fileWriteTimes.ContainsKey(id) || (cachedTextures.ContainsKey(id) && !DateTime.Equals(File.GetLastWriteTimeUtc(file), fileWriteTimes[id]))) {
                    cachedTextures.Remove(id);
                    texturesToLoad.Add(id);
                    layersToLoad.Add(Regex.Replace(id, @"_[^_]+\.", "."));
                    fileWriteTimes[id] = File.GetLastWriteTimeUtc(file);
                    //Dbgl($"adding new {fileName} custom texture.");
                }

                customTextures[id] = file;
            }
        }
        public static List<int> reloadedObjects = new List<int>();
        private static void ReloadTextures(bool locations) {
            reloadedObjects.Clear();
            outputDump.Clear();
            logDump.Clear();

            LoadCustomTextures();

            //Dbgl($"textures to load \n\n{string.Join("\n", texturesToLoad)}");

            ReplaceObjectDBTextures();
            ReplaceSceneObjects();

            Dbgl($"Replaced textures for {reloadedObjects.Count()} found unique objects");

            var zones = SceneManager.GetActiveScene().GetRootGameObjects().Where(go => go.name.StartsWith("_Zone"));

            Dbgl($"Replacing textures for {zones.Count()} zones");
            foreach (var go in zones) {
                ReplaceOneZoneTextures("_GameMain", go);
            }

            ReplaceZoneSystemTextures((ZoneSystem)typeof(ZoneSystem).GetField("m_instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));

            ReplaceHeightmapTextures();

            ReplaceEnvironmentTextures();

            ReplaceZNetSceneTextures();

            if (locations) {
                Dbgl($"Starting ZoneSystem Location prefab replacement");
                stopwatch.Restart();

                ReplaceLocationTextures();

                LogStopwatch("ZoneSystem Locations");
            }

            foreach (Player player in Player.GetAllPlayers()) {
                SetupVisEquipment(player);
            }

            if (logDump.Any())
                Dbgl("\n" + string.Join("\n", logDump));

            Dbgl($"Checked {reloadedObjects.Count} objects total");

            reloadedObjects.Clear();
            if (dumpSceneTextures.Value) {
                string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CustomTextures", "scene_dump.txt");
                Dbgl($"Writing {path}");
                File.WriteAllLines(path, outputDump);
                dumpSceneTextures.Value = false;
            }
        }

        private static void SetupVisEquipment(Humanoid humanoid) {
            try {
                VisEquipment ve = AccessTools.Field(typeof(Humanoid), "m_visEquipment")?.GetValue(humanoid) as VisEquipment;
                if (ve == null)
                    return;

                SetEquipmentTexture(GetVisItemName(ve, "m_leftItem"), GetVisGameObject(ve, "m_leftItemInstance"));
                SetEquipmentTexture(GetVisItemName(ve, "m_rightItem"), GetVisGameObject(ve, "m_rightItemInstance"));
                SetEquipmentTexture(GetVisItemName(ve, "m_helmetItem"), GetVisGameObject(ve, "m_helmetItemInstance"));
                SetEquipmentTexture(GetVisItemName(ve, "m_leftBackItem"), GetVisGameObject(ve, "m_leftBackItemInstance"));
                SetEquipmentTexture(GetVisItemName(ve, "m_rightBackItem"), GetVisGameObject(ve, "m_rightBackItemInstance"));
                SetEquipmentListTexture(GetVisItemName(ve, "m_shoulderItem"), GetVisGameObjectList(ve, "m_shoulderItemInstances"));
                SetEquipmentListTexture(GetVisItemName(ve, "m_utilityItem"), GetVisGameObjectList(ve, "m_utilityItemInstances"));
                SetEquipmentListTexture(GetVisItemName(ve, "m_trinketItem"), GetVisGameObjectList(ve, "m_trinketItemInstances"));
                SetBodyEquipmentTexture(ve, GetVisItemName(ve, "m_legItem"), ve.m_bodyModel, GetVisGameObjectList(ve, "m_legItemInstances"));
                SetBodyEquipmentTexture(ve, GetVisItemName(ve, "m_chestItem"), ve.m_bodyModel, GetVisGameObjectList(ve, "m_chestItemInstances"));
            } catch (Exception ex) {
                Dbgl($"SetupVisEquipment error: {ex}");
            }
        }

        private static object GetVisField(VisEquipment ve, string fieldName) {
            return AccessTools.Field(typeof(VisEquipment), fieldName)?.GetValue(ve);
        }

        private static string GetVisItemName(VisEquipment ve, string fieldName) {
            object stored = GetVisField(ve, fieldName);
            if (stored is string name)
                return string.IsNullOrEmpty(name) ? null : name;
            if (stored is int hash && hash != 0 && ZNetScene.instance != null) {
                GameObject prefab = ZNetScene.instance.GetPrefab(hash);
                return prefab != null ? prefab.name : null;
            }
            return null;
        }

        private static GameObject GetVisGameObject(VisEquipment ve, string fieldName) {
            return GetVisField(ve, fieldName) as GameObject;
        }

        private static List<GameObject> GetVisGameObjectList(VisEquipment ve, string fieldName) {
            object stored = GetVisField(ve, fieldName);
            if (stored is List<GameObject> list)
                return list;
            if (stored is GameObject[] array)
                return array.ToList();
            return null;
        }
    }
}