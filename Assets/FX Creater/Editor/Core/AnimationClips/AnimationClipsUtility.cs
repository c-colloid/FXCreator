using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace colloid.FXCreater.Utility
{
public sealed class AnimationClipsUtility
{
	private AnimationClipsUtility(){
		KeyValue = 0f;
		Key = new Keyframe(0f,KeyValue);
		Curve = new AnimationCurve(Key);
	}
	
	static float KeyValue = 0f;
	static Keyframe Key = new Keyframe();
	static AnimationCurve Curve = new AnimationCurve();
	
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
	
	#region CreateAnimationClip
	public static void CreateAnimationClip(AnimationClip Clip, GameObject Avatar){
		//AnimationClip defaultclip = new AnimationClip();
		var KeyValue = 0f;
		var Key = new Keyframe(0f,KeyValue);
		var Curve = new AnimationCurve(Key);
		foreach (var SMR in Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			//SkinnedMeshRendererからのアニメーションパス
			var SMRPath = GetObjectPath(SMR.gameObject);
        	
			//GameObjectアニメーション
			CreateIsActiveAnimation(SMR.gameObject,Clip,SMRPath);
        	
			//BlendShapeアニメーション
			CreateBlendShapeAnimation(SMR,Clip,SMRPath);
		}
		
		//Clip保存(テスト用)
		//AssetDatabase.CreateAsset(defaultclip,"Assets/defaultanim.anim");
	}
	
	public static void CreateAnimationClip(AnimationClip Clip, List<SkinnedMeshRenderer> TargetSkinList){
		//AnimationClip defaultclip = new AnimationClip();
		KeyValue = 0f;
		Key = new Keyframe(0f,KeyValue);
		Curve = new AnimationCurve(Key);
		foreach (var SMR in TargetSkinList)
		{
			var SMRPath = GetObjectPath(SMR.gameObject);
        	
			//GameObjectアニメーション
			CreateIsActiveAnimation(SMR.gameObject,Clip,SMRPath);
        	
			//BlendShapeアニメーション
			CreateBlendShapeAnimation(SMR,Clip,SMRPath);
		}
		
		//Clip保存(テスト用)
		//AssetDatabase.CreateAsset(defaultclip,"Assets/defaultanim.anim");
	}
	
	public static void CreateAnimationClip(AnimationClip Clip, List<GameObject> TargetList){
		AnimationClip defaultclip = new AnimationClip();
		var KeyValue = 0f;
		var Key = new Keyframe(0f,KeyValue);
		var Curve = new AnimationCurve(Key);
		foreach (var Target in TargetList)
		{
			//SkinnedMeshRendererからのアニメーションパス
			var SMRPath = GetObjectPath(Target);
        	
			//GameObjectアニメーション
			CreateIsActiveAnimation(Target,Clip,SMRPath);
        	
			//BlendShapeアニメーション
			CreateBlendShapeAnimation(Target.GetComponent<SkinnedMeshRenderer>(),Clip,SMRPath);
		}
		
		//Clip保存(テスト用)
		//AssetDatabase.CreateAsset(defaultclip,"Assets/defaultanim.anim");
	}
	
	static string GetObjectPath(GameObject Target)
	{
		//SkinnedMeshRendererからのアニメーションパス
		var SMRPath = new StringBuilder(Target.name);
		var current = Target.transform.parent;
		while (current != null)
		{
			if (current.TryGetComponent(out Animator animator)) break;
			SMRPath.Insert(0, $"{current.name}/");
			current = current.parent;
		}
		return SMRPath.ToString();
	}
	
	static void CreateIsActiveAnimation(GameObject Target, AnimationClip TargetClip, string SMRPath)
	{
		KeyValue = Convert.ToInt16(Target.active);
		Key.value = KeyValue;
		Curve.ClearKeys();
		Curve.AddKey(Key);
		TargetClip.SetCurve(SMRPath,typeof(GameObject),$"m_IsActive",Curve);
	}
	
	static void CreateBlendShapeAnimation(SkinnedMeshRenderer SMR, AnimationClip TargetClip, string SMRPath)
	{
		for (int i = 0; i < SMR.sharedMesh.blendShapeCount; i++) 
		{
			KeyValue = SMR.GetBlendShapeWeight(i);
			Key.value = KeyValue;
			Curve.ClearKeys();
			Curve.AddKey(Key);
			TargetClip.SetCurve(SMRPath,typeof(SkinnedMeshRenderer),$"blendShape.{SMR.sharedMesh.GetBlendShapeName(i)}",Curve);
		}
	}
	#endregion
	
	#region SaveClipFile
	public static AnimationClip SaveNewClip(string Name, string Path)
	{
		var NewClipPath = EditorUtility.SaveFilePanelInProject("Save new AnimationClip",$"{Name}","anim","",string.IsNullOrEmpty(Path) ? "Assets" : Path);
		if (string.IsNullOrEmpty(NewClipPath)) return null;
		var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(NewClipPath);
		if (newClip == null)
		{
			AssetDatabase.CreateAsset(newClip = new AnimationClip(),NewClipPath);	
		}
		return newClip;
	}
	
	public static AnimationClip SaveNewClip(string Name, string Path, AnimationClip SaveClip)
	{
		var NewClipPath = EditorUtility.SaveFilePanelInProject("Save new AnimationClip",$"{Name}","anim","",string.IsNullOrEmpty(Path) ? "Assets" : Path);
		if (string.IsNullOrEmpty(NewClipPath)) return null;
		var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(NewClipPath);
		if (newClip == null)
		{
			AssetDatabase.CreateAsset(SaveClip,NewClipPath);	
		}
		return newClip;
	}
	#endregion
}
}