using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ARK.EditorTools.Addressable;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

namespace ARK.EditorTools.Build
{
    /// <summary>
    /// Build project with menu item and provide method for CI auto build.
    /// </summary>
    public class ProjectBuilder
    {

        public enum ServerConfig
        {

            Dev_Server,
            Production_Server

        }

        public static void UpdateAddressableUseEditor(BuildSetting.DefineSymbolConfig buildSetting)
        {
            var server = buildSetting == BuildSetting.DefineSymbolConfig.Dev ? ServerConfig.Dev_Server : ServerConfig.Production_Server;
            Debug.Log($"[UpdateAddressable] use Profile {server}");
            AddressableProfileUtility.SetAddressableProfile(server.ToString());

            // 可以在這邊做版本更改所需要的步驟
            // GameFileSetup.ChangeFileVersion(buildSetting.symbolConfig);
            UpdateAddressableFlow();
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        private static void UpdateAddressableFlow()
        {
            AssetDatabase.Refresh();
            Debug.Log("Clean Addressable Library");

            AddressableAssetSettings.CleanPlayerContent(null);
            BuildCache.PurgeCache(false);

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("Build Addressable Library");
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("Finished Addressable Library");
        }

        static void BuildProject(BuildTarget buildTarget, BuildSetting buildSetting)
        {
            BuildReport buildReport;
            string      defineSymbolBeforeBuild = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(buildTarget));

            var optionsList = buildSetting.symbolConfig == BuildSetting.DefineSymbolConfig.Production
                ? BuildOptions.None        | BuildOptions.CleanBuildCache
                : BuildOptions.Development | BuildOptions.CleanBuildCache;

            EnvironmentSetting(buildTarget, buildSetting);


            BuildPlayerOptions buildPlayerOption = new BuildPlayerOptions
            {
                scenes           = EditorBuildSettings.scenes.Where((s) => s.enabled).Select((s) => s.path).ToArray(),
                locationPathName = GetBuildPath(buildTarget, buildSetting.symbolConfig, buildSetting.outputPath),
                target           = buildTarget,
                options          = optionsList,
            };

            var defines = BuildSetting.symbolConfigToDefineSymbol[buildSetting.symbolConfig];
            if(buildSetting.whiteList)
            {
                defines += ";WhiteList";
            }
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                BuildPipeline.GetBuildTargetGroup(buildTarget),
                defines
            );

            AssetDatabase.Refresh();


            buildReport = BuildPipeline.BuildPlayer(buildPlayerOption);


            var backUpThisFolderPath
                = $"{buildSetting.outputPath}_BackUpThisFolder_ButDontShipItWithYourGame";

            Debug.Log($"[BackUpThisFolderPath] {backUpThisFolderPath}");
            if(!Directory.Exists(backUpThisFolderPath))
                return;

            Directory.Delete(backUpThisFolderPath, true);


            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                BuildPipeline.GetBuildTargetGroup(buildTarget), defineSymbolBeforeBuild);


            if(buildReport.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[ProjectBuilder] Build Success: Time:" + buildReport.summary.totalTime + " Size:" + buildReport.summary.totalSize + " bytes");
                if(InternalEditorUtility.inBatchMode)
                    EditorApplication.Exit(0);
            }
            else
            {
                if(InternalEditorUtility.inBatchMode)
                    EditorApplication.Exit(1);
                throw new Exception("[ProjectBuilder] Build Failed: Time:" + buildReport.summary.totalTime + " Total Errors:" + buildReport.summary.totalErrors);
            }
        }

        private static void EnvironmentSetting(BuildTarget buildTarget, BuildSetting buildSetting)
        {
            switch(buildTarget)
            {
                case BuildTarget.iOS:
                    Setting(buildSetting);
                    break;

                case BuildTarget.Android:
                    Setting(buildSetting);
                    break;
            }
        }

        static string GetBuildPath(BuildTarget buildTarget, BuildSetting.DefineSymbolConfig defineSymbolConfig, string outputPath = "")
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string timeStamp   = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string fileName    = $"{PlayerSettings.productName}_{defineSymbolConfig}_{timeStamp}{GetFileExtension(buildTarget)}";
            string buildPath;
            string version = PlayerSettings.bundleVersion;


            outputPath = (outputPath == "") ? desktopPath : outputPath;

            // buildPath = Path.Combine(outputPath, PlayerSettings.productName, $"{buildTarget}_{timeStamp}", fileName);

            buildPath = outputPath + GetFileExtension(buildTarget);

            buildPath = buildPath.Replace(@"\", @"\\");

            return buildPath;
        }

        private static void Setting(BuildSetting buildSetting)
        {
            var keyaliasName = "com.demo.test";
            #if UNITY_EDITOR_OSX
            PlayerSettings.Android.keystoreName = "/Users/demo.keystore";
            PlayerSettings.Android.keyaliasName = keyaliasName;
            PlayerSettings.Android.keyaliasPass = "demo";
            PlayerSettings.Android.keystorePass = "demo";
            #endif

            #if UNITY_EDITOR_WIN
            PlayerSettings.Android.keystoreName = "C:/Users/demo.keystore";
            PlayerSettings.Android.keyaliasName = keyaliasName;
            PlayerSettings.Android.keyaliasPass = "demo";
            PlayerSettings.Android.keystorePass = "demo";
            #endif

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            if(buildSetting.androidBuildType == BuildSetting.AndroidBuildType.aab)
            {
                PlayerSettings.Android.useAPKExpansionFiles = true;
                EditorUserBuildSettings.buildAppBundle      = true;
            }
            else
            {
                PlayerSettings.Android.useAPKExpansionFiles = false;
                EditorUserBuildSettings.buildAppBundle      = false;
            }

            if(buildSetting.symbolConfig is BuildSetting.DefineSymbolConfig.Release or BuildSetting.DefineSymbolConfig.Production)
                EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
            else
                EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;

            if(buildSetting.symbolConfig == BuildSetting.DefineSymbolConfig.Production)
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, keyaliasName);
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS,     keyaliasName);
            }
            else
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, keyaliasName);
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS,     keyaliasName);
            }
        }

        private static Dictionary<string, string> ParseCommandLineArgument()
        {
            Dictionary<string, string> commandLineArgToValue = new Dictionary<string, string>();
            string[] customCommandLineArg =
            {
                "-outputPath", "-defineSymbolConfig",
                "-logFile", "-androidBuildType", "-whiteList", "-local"
            };
            string[] commandLineArg = Environment.GetCommandLineArgs();

            for(var i = 0; i < commandLineArg.Length; i++)
            {
                for(var j = 0; j < customCommandLineArg.Length; j++)
                    if(commandLineArg[i] == customCommandLineArg[j])
                        commandLineArgToValue.Add(customCommandLineArg[j], commandLineArg[(i + 1) % commandLineArg.Length]);
            }

            return commandLineArgToValue;
        }

        private static string GetFileExtension(BuildTarget target)
        {
            switch(target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return ".exe";
                case BuildTarget.StandaloneOSX:
                    return ".app";
                case BuildTarget.StandaloneLinux64:
                    return ".x86_64";
                case BuildTarget.WebGL:
                    return ".webgl";
                case BuildTarget.Android:
                    return EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
                case BuildTarget.iOS:
                    return "";
                default:
                    Debug.LogError("No corresponding extension!");
                    return "";
            }
        }

        public static void SwitchPlatform(BuildTarget targetPlatform)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(targetPlatform), targetPlatform);
        }

    }

    public class BuildSetting
    {

        public enum DefineSymbolConfig
        {

            Dev        = 0,
            Release    = 1,
            Production = 2

        }

        public enum AndroidBuildType
        {

            apk,
            aab,

        }

        public static readonly Dictionary<DefineSymbolConfig, string> symbolConfigToDefineSymbol = new Dictionary<DefineSymbolConfig, string>
        {
            [DefineSymbolConfig.Dev]        = "ODIN_INSPECTOR_3;dUI_MANAGER;dUI_TextMeshPro;UNITY_POST_PROCESSING_STACK_V2;ODIN_VALIDATOR;Dev;",
            [DefineSymbolConfig.Release]    = "ODIN_INSPECTOR_3;dUI_MANAGER;dUI_TextMeshPro;UNITY_POST_PROCESSING_STACK_V2;ODIN_VALIDATOR;Release;",
            [DefineSymbolConfig.Production] = "DISABLE_SRDEBUGGER,ODIN_INSPECTOR_3;dUI_MANAGER;dUI_TextMeshPro;UNITY_POST_PROCESSING_STACK_V2;ODIN_VALIDATOR;Production;"
        };
        public string             outputPath       = "";
        public DefineSymbolConfig symbolConfig     = DefineSymbolConfig.Dev;
        public string             logFile          = "";
        public AndroidBuildType   androidBuildType = AndroidBuildType.apk;
        public bool               whiteList        = false;
        public bool               local            = false;


        public BuildSetting(string outputPath, string logFile, DefineSymbolConfig symbolConfig)
        {
            this.outputPath   = outputPath;
            this.symbolConfig = symbolConfig;
            this.logFile      = logFile;
        }

        public override string ToString()
        {
            return $"{nameof(BuildSetting)}: symbolConfig={symbolConfig}, outputPath={outputPath}";
        }

    }
}