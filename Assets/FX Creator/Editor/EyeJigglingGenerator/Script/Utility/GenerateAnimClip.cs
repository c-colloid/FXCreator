using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Generate AnimationClip asset.
/// </summary>
public class GenerateAnimClip
{
	//privete Constractor
	GenerateAnimClip(){ }
	
	public static void Generate(AnimationCurve curve)
	{
		
	}
	
	public static void Generate(AnimationCurve curve, object target)
	{
		AnimationClip clip = new AnimationClip();
		SkinnedMeshRenderer SMR = target as SkinnedMeshRenderer;
		GameObject Target = SMR.gameObject;
		
		//SkinnedMeshRendererからのアニメーションパス
		var SMRPath = GetPath(Target);

		var Curve = new AnimationCurve();
		int BlendshapeIndex = new int();
		for (int i = 0; i < curve.keys.Length; i++) {
			Curve.AddKey(curve.keys[i]);
			Curve.keys[i].inTangent = curve.keys[i].inTangent;
			Curve.keys[i].outTangent = curve.keys[i].outTangent;
		}
		clip.SetCurve(
			SMRPath,
			typeof(SkinnedMeshRenderer),
			$"blendShape.{SMR.sharedMesh.GetBlendShapeName(BlendshapeIndex)}",
			Curve);
	}
	
	public static void Generate(BlendAnimation data)
	{
		
	}
	
	public static AnimationClip Generate(List<BlendAnimation> data, object target)
	{
		AnimationClip Clip = new AnimationClip();
		SkinnedMeshRenderer SMR = target as SkinnedMeshRenderer;
		GameObject Target = SMR.gameObject;
		
		//SkinnedMeshRendererからのアニメーションパス
		var SMRPath = GetPath(Target);

		//int BlendshapeIndex = new int();
		
		data.ForEach(o => {
			var Curve = new AnimationCurve();
			foreach (var key in o.AnimationCurve.keys)
			{
				Keyframe Key = key;
				Key.value *= 100;
				Curve.AddKey(Key);
			}
			if (!Curve.keys.Any(key => key.time == 0))
			{
				Curve.AddKey(0,0);
			}
			Clip.SetCurve(
				SMRPath,
				typeof(SkinnedMeshRenderer),
				$"blendShape.{o.BlendShape}",
				Curve);
		});
		
		var settings = AnimationUtility.GetAnimationClipSettings(Clip);
		settings.loopTime = true;
		AnimationUtility.SetAnimationClipSettings(Clip, settings);
		
		return Clip;
	}
	
	static string GetPath(GameObject target)
	{
		var Target = target as GameObject;
		var Path = new StringBuilder(Target.name);
		var current = Target.transform.parent;
		while (current != null)
		{
			if (current.TryGetComponent(out Animator animator)) break;
			Path.Insert(0, $"{current.name}/");
			current = current.parent;
		}
		return Path.ToString();
	}
	
	/// <summary>
	///  Create AnimationClip file
	/// </summary>
	/// <param name="clip">AnimationClip data</param>
	/// <param name="path">Save folder path</param>
	public static void Save(AnimationClip clip, string path)
	{
		var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
		if (newClip == null)
		{
			AssetDatabase.CreateAsset(newClip = clip,path);	
		}
	}
}
