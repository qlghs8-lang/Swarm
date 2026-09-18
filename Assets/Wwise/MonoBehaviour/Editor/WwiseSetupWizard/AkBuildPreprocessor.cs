/*******************************************************************************
The content of this file includes portions of the proprietary AUDIOKINETIC Wwise
Technology released in source code form as part of the game integration package.
The content of this file may not be used without valid licenses to the
AUDIOKINETIC Wwise Technology.
Note that the use of the game engine is subject to the Unity(R) Terms of
Service at https://unity3d.com/legal/terms-of-service
 
License Usage
 
Licensees holding valid licenses to the AUDIOKINETIC Wwise Technology may use
this file in accordance with the end user license agreement provided with the
software or, alternatively, in accordance with the terms contained
in a written agreement between you and Audiokinetic Inc.
Copyright (c) 2026 Audiokinetic Inc.
*******************************************************************************/

﻿#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using AK.Wwise.Unity.Logging;

public partial class AkBuildPreprocessor
{
	/// <summary>
	///     User hook called to retrieve the custom platform name used to determine the base path. Do not modify platformName
	///     to use default platform names.
	/// </summary>
	/// <param name="platformName">The custom platform name.</param>
	public delegate void CustomPlatformNameGetter(ref string platformName, UnityEditor.BuildTarget target);

	public static CustomPlatformNameGetter GetCustomPlatformName;

	public delegate void OnBuildCallback(string path);

	public static void BuildCallbackNoOp(string path)
	{
		// No-op
	}

	public class PlatformConfiguration
	{
		public string WwisePlatformName;
		public OnBuildCallback OnPreprocessBuild = BuildCallbackNoOp;
		public OnBuildCallback OnPostprocessBuild = BuildCallbackNoOp;
	}

	private static Dictionary<UnityEditor.BuildTarget, PlatformConfiguration> PlatformConfigurations = new Dictionary<UnityEditor.BuildTarget, PlatformConfiguration>();

	public static void RegisterBuildTarget(UnityEditor.BuildTarget target, PlatformConfiguration config)
	{
		PlatformConfigurations.Add(target, config);
	}

	public static string GetPlatformName(UnityEditor.BuildTarget target)
	{
		var platformSubDir = string.Empty;
		GetCustomPlatformName?.Invoke(ref platformSubDir, target);

		if (!string.IsNullOrEmpty(platformSubDir))
			return platformSubDir;

		//New way to access target platform name
		string platformName = AkBasePathGetter.GetTargetPlatformName(target);
		if (platformName != string.Empty)
		{
			return platformName;
		}
		
		//For legacy code compatibility. Should maybe remove in 26.1 or 27.1 to give time to adjust 
		if (PlatformConfigurations.ContainsKey(target))
		{
			if (platformName == string.Empty)
			{
				platformName = PlatformConfigurations[target].WwisePlatformName;
			}
			return platformName;
		}
		//Keeping the old fallback of using the Unity target name.
		return target.ToString();
	}
}


#if UNITY_2018_1_OR_NEWER
public partial class AkBuildPreprocessor : UnityEditor.Build.IPreprocessBuildWithReport, UnityEditor.Build.IPostprocessBuildWithReport
#else
public partial class AkBuildPreprocessor : UnityEditor.Build.IPreprocessBuild, UnityEditor.Build.IPostprocessBuild
#endif
{
	public int callbackOrder
	{
		get { return 0; }
	}

	private string destinationSoundBankFolder = string.Empty;

	// ── Swarm 로컬 패치 — docs/webgl-build.md §2-5 ─────────────────────────────────────
	//
	// 이 프로젝트에는 Wwise의 WebGL 플랫폼 SDK가 설치되어 있지 않다(Generated/ 에 Windows·Mac만).
	// 그 상태로 WebGL 빌드를 걸면 아래 세 곳이 전부 LogLevel.Error 를 뱉는다.
	//
	//   1. AkBasePathGetter.GetSoundBankPaths  — "Could not find source folder for <WebGL> platform"
	//   2. OnPreprocessBuildInternal           — "SoundBank folder has not been copied for <WebGL>"
	//   3. AkPluginActivator                   — "Unable to find Plugin Activator for Build Target WebGL"
	//
	// Unity는 빌드 전처리기가 뱉은 에러를 빌드 실패로 취급하므로("Error building Player: 3 errors"),
	// 경고가 아니라 실제로 빌드가 0초 만에 멈춘다. WebGL 빌드는 애초에 무음이라(AudioDirector 참조)
	// 이 전처리기가 할 일이 없다 — 그래서 통째로 건너뛴다.
	//
	// ⚠️ Wwise Launcher로 통합을 다시 돌리면 이 파일은 덮어쓰인다. asmdef 4개와 함께 다시 적용할 것.
	private static bool IsWwiseUnsupportedTarget(UnityEditor.BuildTarget target)
	{
		return target == UnityEditor.BuildTarget.WebGL;
	}

	public static bool CopySoundbanks(bool generate, string platformName, ref string destinationFolder)
	{
		if (string.IsNullOrEmpty(platformName))
		{
			WwiseLogger.LogFormat(LogLevel.Error, "Could not determine platform name for <{0}> platform", platformName);
			return false;
		}

		if (generate)
		{
			var platforms = new System.Collections.Generic.List<string> { platformName };
			string wwiseInstallationPath = "";
#if UNITY_EDITOR_WIN
			wwiseInstallationPath = AkWwiseEditorSettings.Instance.WwiseInstallationPathWindows;
#elif UNITY_EDITOR_OSX
			wwiseInstallationPath = AkWwiseEditorSettings.Instance.WwiseInstallationPathMac;
#endif
			AkUtilities.GenerateSoundbanks(wwiseInstallationPath, AkWwiseEditorSettings.WwiseProjectAbsolutePath, platforms);
		}

		string sourceFolder;
		if (!AkBasePathGetter.GetSoundBankPaths(platformName, out sourceFolder, out destinationFolder))
			return false;

		if (!AkUtilities.DirectoryCopy(sourceFolder, destinationFolder, true))
		{
			destinationFolder = null;
			WwiseLogger.LogFormat(LogLevel.Error, "Could not copy SoundBank folder for <{0}> platform", platformName);
			return false;
		}

		WwiseLogger.LogFormat(LogLevel.Log, "Copied SoundBank folder from <{0}> to streaming assets folder <{1}> for <{2}> platform build", sourceFolder, destinationFolder, platformName);
		return true;
	}


	public static void DeleteSoundbanks(string destinationFolder)
	{
		if (string.IsNullOrEmpty(destinationFolder))
			return;

		System.IO.Directory.Delete(destinationFolder, true);
		WwiseLogger.LogFormat(LogLevel.Log, "Deleting streaming assets folder <{0}>", destinationFolder);
	}

	public void OnPreprocessBuildInternal(UnityEditor.BuildTarget target, string path)
	{
		if (IsWwiseUnsupportedTarget(target))
		{
			return;
		}

		var platformName = GetPlatformName(target);
#if !(AK_WWISE_ADDRESSABLES && UNITY_ADDRESSABLES)
		if (AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep)
		{
			if (!CopySoundbanks(AkWwiseEditorSettings.Instance.GenerateSoundBanksAsPreBuildStep, platformName, ref destinationSoundBankFolder))
			{
				WwiseLogger.LogFormat(LogLevel.Error, "SoundBank folder has not been copied for <{0}> target at <{1}>. This will likely result in a build without sound!!!", target, path);
			}
		}
#endif
		if (PlatformConfigurations.TryGetValue(target, out var config))
		{
			config.OnPreprocessBuild(path);
		}
		
		// Init ProjectDB for platform being built
		WwiseProjectDatabase.Init(AkWwiseEditorSettings.GetRootOutputPath(), platformName);
		AkPluginActivator.ForceUpdate();
		AkPluginActivator.ActivatePluginsForDeployment(target, true);
	}

	public void OnPostprocessBuildInternal(UnityEditor.BuildTarget target, string path)
	{
		if (IsWwiseUnsupportedTarget(target))
		{
			return;
		}

		AkPluginActivator.ActivatePluginsForDeployment(target, false);
		if (PlatformConfigurations.TryGetValue(target, out var config))
		{
			config.OnPostprocessBuild(path);
		}
#if !(AK_WWISE_ADDRESSABLES && UNITY_ADDRESSABLES)
		DeleteSoundbanks(destinationSoundBankFolder);
#endif
		destinationSoundBankFolder = string.Empty;
		
		// Point the ProjectDB back on the current editor platform
		WwiseProjectDatabase.Init(AkWwiseEditorSettings.GetRootOutputPath(), AkBasePathGetter.GetPlatformName());
	}

#if UNITY_2018_1_OR_NEWER
	public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
	{
		OnPreprocessBuildInternal(report.summary.platform, report.summary.outputPath);
	}

	public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
	{
		OnPostprocessBuildInternal(report.summary.platform, report.summary.outputPath);
	}
#else
		public void OnPreprocessBuild(UnityEditor.BuildTarget target, string path)
	{
		OnPreprocessBuildInternal(target, path);
	}

	public void OnPostprocessBuild(UnityEditor.BuildTarget target, string path)
	{
		OnPostprocessBuildInternal(target, path);
	}
#endif
}
#endif // #if UNITY_EDITOR
