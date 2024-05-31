using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEditor;

namespace colloid.FXCreator.Utility
{
public sealed class AnimationClipsUtility
{
	private AnimationClipsUtility(){}
	
	public static List<AnimationClip> GetClips(string folderpath)
	{
		var folderitems = Directory.GetFiles(folderpath, "*", SearchOption.AllDirectories);
		List<AnimationClip> clips = new List<AnimationClip>();
		foreach (var item in folderitems)
		{
			var asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(item);
			if (asset != null) clips.Add(asset);
		}
		return clips;
	}
	
	public static AnimationClip GetClip(string itempath)
	{
		var clip = AssetDatabase.LoadAssetAtPath(itempath, typeof(AnimationClip)) as AnimationClip;
		return clip;
	}
}
}