using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ARK.EditorTools.Build;
using ARK.EditorTools.CustomAttribute;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace ARK.EditorTools.Addressable
{
    public enum AssetBundleCrcOptions
    {

        Disabled,
        EnabledIncludingCached,
        EnabledExcludingCached

    }

    public class AddressableGroupEditorWindow : OdinEditorWindow
    {

        private const string TagListPath      = "Assets/AddressableGroupEditor/Resources/TagList.asset";
        private const string DevExportPathKey = "AddressableAssetsDevExportPath";
        private const string ProExportPathKey = "AddressableAssetsProExportPath";

        [MenuItem("ARK_Tools/AddressableGroupWindow")]
        private static void OpenWindow()
        {
            GetWindow<AddressableGroupEditorWindow>().Show();
        }

        [FolderPath(AbsolutePath = true, RequireExistingPath = true), LabelText("[Dev] 熱更輸出路徑")]
        [OnValueChanged(nameof(OnDevExportPathChanged))]
        public string dev_exportPath;

        [FolderPath(AbsolutePath = true, RequireExistingPath = true), LabelText("[Pro] 熱更輸出路徑")]
        [OnValueChanged(nameof(OnProExportPathChanged))]
        public string pro_exportPath;

        [ShowInInspector, EnumToggleButtons, LabelText("版本設定")]
        public BuildSetting.DefineSymbolConfig symbolConfig;

        [InlineEditor(InlineEditorObjectFieldModes.Boxed), HideLabel]
        public AddressableTagList addressableTagList;

        [ShowInInspector, TableList(AlwaysExpanded = true), PropertyOrder(10), HideLabel]
        private List<GroupEntry> groupEntries;

        private List<GroupEntry> initialGroupEntries;

        private AddressableAssetSettings settings;


        protected override void OnEnable()
        {
            base.OnEnable();

            // 在窗口打開時從 EditorPrefs 中加載值
            dev_exportPath = EditorPrefs.GetString(DevExportPathKey, string.Empty);
            pro_exportPath = EditorPrefs.GetString(ProExportPathKey, string.Empty);
        }

        private void OnDevExportPathChanged()
        {
            // 當 dev_exportPath 被更改時，保存到 EditorPrefs
            EditorPrefs.SetString(DevExportPathKey, dev_exportPath);
        }

        private void OnProExportPathChanged()
        {
            // 當 pro_exportPath 被更改時，保存到 EditorPrefs
            EditorPrefs.SetString(ProExportPathKey, pro_exportPath);
        }

        [Button("1.建置Android素材", ButtonSizes.Large), HorizontalGroup("Addressable")]
        public void BuildAndroidAddressable()
        {
            Debug.Log("[Build] Switch Platform to Android");

            //換平台
            ProjectBuilder.SwitchPlatform(BuildTarget.Android);

            //設定目前應該建置的版本
            Debug.Log($"[Build] Set Build Setting to {symbolConfig}");
            ProjectBuilder.UpdateAddressableUseEditor(symbolConfig);
        }

        [Button("2.建置iOS素材", ButtonSizes.Large), HorizontalGroup("Addressable")]
        public void BuildIOSAddressable()
        {
            Debug.Log("[Build] Switch Platform to iOS");
            ProjectBuilder.SwitchPlatform(BuildTarget.iOS);
            Debug.Log($"[Build] Set Build Setting to  {symbolConfig}");
            ProjectBuilder.UpdateAddressableUseEditor(symbolConfig);
        }

        [Button("3.將雙平台資料移至指定路徑", ButtonSizes.Large), HorizontalGroup("Addressable")]
        public void MoveAllPlatformCatalogFilesToServerPath()
        {
            // 將熱更新後的資料搬移到後端路徑，註解為曾經使用的方法，但不列出
            // MoveCatalogFileToServerPath(AddressableExtraSetup.Platform.Android);
            // MoveAddressableContentBinToServerPath(AddressableExtraSetup.Platform.Android);
            // MoveCatalogFileToServerPath(AddressableExtraSetup.Platform.iOS);
            // MoveAddressableContentBinToServerPath(AddressableExtraSetup.Platform.iOS);
        }

        [Button("一鍵建置", ButtonSizes.Large), GUIColor(0, 1, 0), HorizontalGroup("Addressable")]
        public void BuildAddressableContent()
        {
            BuildAndroidAddressable();
            BuildIOSAddressable();
            MoveAllPlatformCatalogFilesToServerPath();
        }

        protected override void Initialize()
        {
            base.Initialize();
            LoadOrCreateTagList();
            LoadAddressableGroups();
            SaveInitialGroupEntries();
        }

        private void SaveInitialGroupEntries()
        {
            initialGroupEntries = groupEntries.Select(entry => entry.Clone()).ToList();
        }

        private void LoadOrCreateTagList()
        {
            // 新增目錄檢查與建立
            string directory = Path.GetDirectoryName(TagListPath);
            if(!AssetDatabase.IsValidFolder(directory))
            {
                string parentFolder = Path.GetDirectoryName(directory);
                string newFolderName = Path.GetFileName(directory);
                AssetDatabase.CreateFolder(parentFolder, newFolderName);
            }

            addressableTagList = AssetDatabase.LoadAssetAtPath<AddressableTagList>(TagListPath);

            if(addressableTagList == null)
            {
                // 如果 tagList 不存在，創建新的並保存到指定路徑
                addressableTagList = CreateInstance<AddressableTagList>();
                
                AssetDatabase.CreateAsset(addressableTagList, TagListPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("TagList asset created at: " + TagListPath);
            }
        }

        private void LoadAddressableGroups()
        {
            settings     = AddressableAssetSettingsDefaultObject.Settings;
            groupEntries = new List<GroupEntry>();
            //if setting is null , show log to user
            if(settings == null)
            {
                Debug.LogError("addressable group not set");
                return;
            }

            foreach(var group in settings.groups)
            {
                if(group != null)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    if(schema != null)
                    {
                        var crcOptions = schema.UseAssetBundleCrc
                            ? (schema.UseAssetBundleCrcForCachedBundles ? AssetBundleCrcOptions.EnabledIncludingCached : AssetBundleCrcOptions.EnabledExcludingCached)
                            : AssetBundleCrcOptions.Disabled;

                        groupEntries.Add(new GroupEntry
                        {
                            GroupName    = group.Name,
                            BuildPath    = schema.BuildPath.GetName(settings),
                            LoadPath     = schema.LoadPath.GetName(settings),
                            Labels       = group.entries.SelectMany(e => e.labels).Distinct().ToArray(),
                            CrcOptions   = crcOptions,
                            EditorWindow = this // 傳遞編輯器實例
                        });
                    }
                }
            }
            SaveInitialGroupEntries(); // 儲存初始值
        }

        #region 編輯器相關
        [Button("存檔", ButtonSizes.Large)]
        [HorizontalGroup("Addressable", Width = 100)]
        [GUIColor(0.7f, 0f, 0f)] // 設定為偏深紅色
        private void ApplyChangesToAllGroups()
        {
            foreach(var groupEntry in groupEntries)
            {
                var group = settings.FindGroup(groupEntry.GroupName);
                if(group != null)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    if(schema != null)
                    {
                        schema.UseAssetBundleCrc                 = groupEntry.CrcOptions != AssetBundleCrcOptions.Disabled;
                        schema.UseAssetBundleCrcForCachedBundles = groupEntry.CrcOptions == AssetBundleCrcOptions.EnabledIncludingCached;
                        schema.BuildPath.SetVariableByName(settings, groupEntry.BuildPath);
                        schema.LoadPath.SetVariableByName(settings, groupEntry.LoadPath);

                        // 更新 Group 的 Labels
                        foreach(var entry in group.entries)
                        {
                            if(groupEntry.Labels != null)
                            {
                                foreach(var label in groupEntry.Labels)
                                {
                                    entry.SetLabel(label, true);
                                }
                            }
                        }

                        EditorUtility.SetDirty(settings);
                    }
                }
            }
            AssetDatabase.SaveAssets();
            SaveInitialGroupEntries(); // 更新初始值

            Debug.Log("Changes applied to all groups.");
        }

        [Button("還原", ButtonSizes.Large)]
        [HorizontalGroup("Addressable", Width = 100)]
        [GUIColor(0f, 0.5f, 0f)] // 設定為偏深綠色
        private void RevertChanges()
        {
            // 將 groupEntries 重置為初始狀態
            LoadAddressableGroups();
            Debug.Log("Groups reloaded.");
        }

        public GroupEntry GetInitialEntry(GroupEntry currentEntry)
        {
            return initialGroupEntries.FirstOrDefault(entry => entry.GroupName == currentEntry.GroupName);
        }
        #endregion

        public string GetFirstLevelDirectory(string platform, string assetPath)
        {
            // 獲取從 "Android/" 開始的部分路徑
            var parts = assetPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            // 找到 "Android" 並獲取下一個目錄名稱
            for(var i = 0; i < parts.Length; i++)
            {
                if(parts[i].Equals(platform, StringComparison.OrdinalIgnoreCase))
                {
                    if(i + 1 < parts.Length)
                    {
                        var firstLevelDirectory = parts[i + 1];
                        return firstLevelDirectory;
                    }
                }
            }

            Debug.LogWarning($"No first level directory found under'{platform}' in the given path.");
            return null;
        }

        public static void EnsureDirectoryExists(string filePath)
        {
            var directoryPath = Path.GetDirectoryName(filePath);

            Directory.CreateDirectory(directoryPath);
        }


        /// <summary>
        /// 從遠端素材路徑中獲取本地素材路徑
        /// </summary>
        /// <param name="targetServerAssetPath"></param>
        /// <param name="serverBuildPath"></param>
        /// <returns></returns>
        private static List<string> GetLocalAssetsPathFromRemoteBuild(List<string> targetServerAssetPath, string serverBuildPath)
        {
            var moveFolder = new List<string>();
            foreach(var asset in targetServerAssetPath)
            {
                //use targetAsset name , get same with build path
                var projectRootPath = Path.GetDirectoryName(Application.dataPath);
                var parentPath      = Path.Combine(projectRootPath, serverBuildPath);
                var assetPath       = parentPath + asset;

                if(moveFolder.Contains(assetPath))
                {
                    continue;
                }

                moveFolder.Add(assetPath);
            }
            return moveFolder;
        }

        private List<string> GetServerAssets(ContentCatalogData contentCatalogData, string serverLoadPath)
        {
            var catalogDataType       = typeof(ContentCatalogData);
            var internalAssetField    = catalogDataType.GetField("m_InternalIds", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetServerAssetPath = new List<string>();

            if(internalAssetField != null)
            {
                //獲取所有要載入的素材
                var assetFields = (string[])(internalAssetField.GetValue(contentCatalogData));
                foreach(var assetPath in assetFields)
                {
                    //如果確定這個素材是要熱更新的
                    if(assetPath.StartsWith(serverLoadPath))
                    {
                        //只獲取後面素材的片段
                        var singleAssetPath = assetPath.Substring(serverLoadPath.Length);
                        targetServerAssetPath.Add(singleAssetPath);
                    }
                }
            }
            else
            {
                Debug.LogError("Internal Asset Field not found.");
            }
            return targetServerAssetPath;
        }

        public string GetTargetPath(string folder, string serverDataPath, string dev_exportPath)
        {
            var index               = folder.IndexOf(serverDataPath, StringComparison.Ordinal);
            var pathAfterServerData = folder[(index + serverDataPath.Length)..];
            var pathC               = Path.Combine(dev_exportPath, pathAfterServerData);
            return pathC;
        }

        public void MoveAndReplaceFile(string sourcePath, string targetPath)
        {
            EnsureDirectoryExists(targetPath);
            if(File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            FileUtil.CopyFileOrDirectory(sourcePath, targetPath);
        }


        [Serializable]
        public class GroupEntry
        {
            /// <summary>
            /// 可以在這修改Group應該展示的內容
            /// </summary>

            [ValueDropdown("GetGroupNames")]
            public string GroupName;

            [GUIColor(nameof(GetColorForBuildPath))]
            [ValueDropdown("GetAvailableBuildPaths")]
            [TableColumnWidth(150, Resizable = false)]
            public string BuildPath;

            [GUIColor(nameof(GetColorForLoadPath))]
            [ValueDropdown("GetAvailableLoadPaths")]
            [TableColumnWidth(150, Resizable = false)]
            public string LoadPath;

            [GUIColor(nameof(GetColorForLabels)), AddressableLabelsTag]
            public string[] Labels;

            [GUIColor(nameof(GetColorForCrcOptions))]
            [TableColumnWidth(200, Resizable = false)]
            public AssetBundleCrcOptions CrcOptions;

            // 將編輯器實例保存到每個 GroupEntry 中
            public AddressableGroupEditorWindow EditorWindow { get; set; }

            public GroupEntry Clone()
            {
                return new GroupEntry
                {
                    GroupName  = this.GroupName,
                    BuildPath  = this.BuildPath,
                    LoadPath   = this.LoadPath,
                    Labels     = this.Labels?.ToArray(),
                    CrcOptions = this.CrcOptions
                };
            }

            [Button("active"), HideLabel]
            [GUIColor(0.5f, 0.8f, 0.5f)] // 可選的按鈕顏色設置
            [TableColumnWidth(60, Resizable = false)]
            private void Select()
            {
                // 更新選中的物件並在 Inspector 顯示
                Selection.activeObject = EditorWindow.settings.FindGroup(GroupName);
            }

            private List<string> GetGroupNames()
            {
                return AddressableAssetSettingsDefaultObject.Settings.groups.Select(g => g.Name).ToList();
            }

            private List<string> GetAvailableBuildPaths()
            {
                var settings        = AddressableAssetSettingsDefaultObject.Settings;
                var profileSettings = settings.profileSettings;

                // 获取当前 Profile 中所有的变量名称
                var variableNames = profileSettings.GetVariableNames();

                return variableNames.ToList();
            }

            private List<string> GetAvailableLoadPaths()
            {
                var settings        = AddressableAssetSettingsDefaultObject.Settings;
                var profileSettings = settings.profileSettings;

                // 获取当前 Profile 中所有的变量名称
                var variableNames = profileSettings.GetVariableNames();

                return variableNames.ToList();
            }


            public Color GetColorForBuildPath()
            {
                return IsBuildPathChanged() ? Color.red : Color.white;
            }

            public Color GetColorForLoadPath()
            {
                return IsLoadPathChanged() ? Color.red : Color.white;
            }

            public Color GetColorForLabels()
            {
                return AreLabelsChanged() ? Color.red : Color.white;
            }

            public Color GetColorForCrcOptions()
            {
                return IsCrcOptionsChanged() ? Color.red : Color.white;
            }

            private bool IsBuildPathChanged()
            {
                var initialEntry = EditorWindow.GetInitialEntry(this);
                return initialEntry != null && initialEntry.BuildPath != BuildPath;
            }

            private bool IsLoadPathChanged()
            {
                var initialEntry = EditorWindow.GetInitialEntry(this);
                return initialEntry != null && initialEntry.LoadPath != LoadPath;
            }

            private bool AreLabelsChanged()
            {
                var initialEntry = EditorWindow.GetInitialEntry(this);
                return initialEntry != null && !Labels.SequenceEqual(initialEntry.Labels);
            }

            private bool IsCrcOptionsChanged()
            {
                var initialEntry = EditorWindow.GetInitialEntry(this);
                return initialEntry != null && initialEntry.CrcOptions != CrcOptions;
            }

        }

    }
}