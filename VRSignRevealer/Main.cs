using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace VRSignRevealer;

public static class Main
{
	private static readonly Dictionary<LODGroup, float> initialScreenRelativeTransitionHeights = new();
#if RELEASE
#pragma warning disable CS0649 // It's expected that LogDebug will never be assigned in Release builds
#endif
	private static Action<string>? LogDebug;
#pragma warning restore CS0649
	private static Action? patchOnLoadingFinished;

	// Unity Mod Manage Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager
	private static bool Load(UnityModManager.ModEntry modEntry)
	{
#if DEBUG
		LogDebug = modEntry.Logger.Log;
#endif

		if (!VRManager.IsVREnabled())
		{
			LogDebug?.Invoke($"In non-vr mode. Won't patch for {modEntry.Info.DisplayName}");
			return true;
		}

		modEntry.OnToggle = OnToggle;
		return true;
	}

	private static bool OnToggle(UnityModManager.ModEntry modEntry, bool isTogglingOn)
	{
		LogDebug?.Invoke($"Toggle {(isTogglingOn ? "on" : "off")} requested for {modEntry.Info.DisplayName}");
		bool result = true;

		if (isTogglingOn)
		{
			patchOnLoadingFinished ??= () => DoPatch(modEntry);
			WorldStreamingInit.LoadingFinished += patchOnLoadingFinished;
			if (WorldStreamingInit.IsLoaded)
			{
				result = DoPatch(modEntry);
			}
		}
		else
		{
			if (patchOnLoadingFinished != null)
			{
				WorldStreamingInit.LoadingFinished -= patchOnLoadingFinished;
			}
			if (WorldStreamingInit.IsLoaded)
			{
				result = DoUnpatch(modEntry);
			}
		}

		return result;
	}

	private static bool DoPatch(UnityModManager.ModEntry modEntry)
	{
		LogDebug?.Invoke($"Doing patch for {modEntry.Info.DisplayName}");

		try
		{
			var trackIdObjects = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name == "[TrackID]");
			LogDebug?.Invoke($"Found {trackIdObjects.Count()} track ID objects");

			foreach (GameObject trackId in trackIdObjects)
			{
				Transform? cursor = trackId.transform;
				while (cursor != null && cursor.gameObject.GetComponent<LODGroup>() is null) { cursor = cursor.parent; }
				if (cursor?.gameObject.GetComponent<LODGroup>() is LODGroup lodGroup)
				{
					LOD[] lods = lodGroup.GetLODs();
					LOD lod0 = lods[0];
					LOD lod1 = lods[1];
					initialScreenRelativeTransitionHeights[lodGroup] = lod0.screenRelativeTransitionHeight;
					lods[0] = new LOD((lod0.screenRelativeTransitionHeight + lod1.screenRelativeTransitionHeight) / 2f, lod0.renderers);
					lodGroup.SetLODs(lods);
				}
			}

			LogDebug?.Invoke($"Patched {initialScreenRelativeTransitionHeights.Count} LOD groups");
		}
		catch (Exception ex)
		{
			modEntry.Logger.LogException($"Failed to load {modEntry.Info.DisplayName}:", ex);
			modEntry.Enabled = false;
			return false;
		}

		return true;
	}

	private static bool DoUnpatch(UnityModManager.ModEntry modEntry)
	{
		LogDebug?.Invoke($"Doing unpatch for {modEntry.Info.DisplayName}");

		try
		{
			foreach (var lodGroupToValue in initialScreenRelativeTransitionHeights)
			{
				LODGroup lodGroup = lodGroupToValue.Key;
				float initialScreenRelativeTransitionHeight = lodGroupToValue.Value;
				LOD[] lods = lodGroup.GetLODs();
				LOD lod0 = lods[0];
				lods[0] = new LOD(initialScreenRelativeTransitionHeight, lod0.renderers);
				lodGroup.SetLODs(lods);
			}

			LogDebug?.Invoke($"Unpatched {initialScreenRelativeTransitionHeights.Count} LOD groups");
			initialScreenRelativeTransitionHeights.Clear();
		}
		catch (Exception ex)
		{
			modEntry.Logger.LogException($"Failed to unload {modEntry.Info.DisplayName}:", ex);
			modEntry.Enabled = false;
			return false;
		}

		return true;
	}
}
