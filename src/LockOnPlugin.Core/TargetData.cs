﻿using BepInEx;
using ParadoxNotion.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

namespace LockOnPlugin.Core
{
    internal class TargetData
    {
        internal static TargetData data;
        private const string dataFileName = "LockOnPluginData.json";

        public static void LoadData()
        {
            var dataPath = Path.Combine(Paths.ConfigPath, dataFileName);

            if(File.Exists(dataPath))
            {
                try
                {
                    var json = File.ReadAllText(dataPath);
                    data = JSONSerializer.Deserialize<TargetData>(json);
                    LockOnPluginCore.Logger.LogInfo("Loading custom target data.");
                }
                catch(Exception)
                {
                    LockOnPluginCore.Logger.LogInfo("Failed to deserialize custom target data. Loading default target data.");
                    LoadResourceData();
                }
            }
            else
            {
                LockOnPluginCore.Logger.LogInfo("Loading default target data.");
                LoadResourceData();
            }
        }

        private static void LoadResourceData()
        {
            var ass = typeof(LockOnPluginCore).Assembly;
            var resourceName = $"{ass.GetName().Name}.{dataFileName}";

            using(var stream = ass.GetManifestResourceStream(resourceName))
            {
                using(var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    data = JSONSerializer.Deserialize<TargetData>(json);
                }
            }
        }

#pragma warning disable 649 // disable never assigned warning
        public List<string> quickTargets;
        public List<CustomTarget> customTargets;
        public List<CenterWeigth> centerWeigths;
        public List<string> presenceTargets;

        public class CustomTarget
        {
            public string target;
            public string point1;
            public string point2;
            public float midpoint;
        }

        public class CenterWeigth
        {
            public string bone;
            public float weigth;
        }
#pragma warning restore 649
    }
}
