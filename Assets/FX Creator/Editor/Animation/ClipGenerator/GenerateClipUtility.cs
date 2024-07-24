using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace colloid.FXCreator.Animation.Utility
{
	public static class GenerateClipUtility
	{
		public static string GetTargetPathFromAvatar(GameObject GO)
		{
			var TargetPath = new StringBuilder(GO.transform.name);
			var Parent = GO.transform.parent;
			while (Parent != null)
			{
				if (Parent.TryGetComponent(out Animator animator)) break;
				TargetPath.Insert(0, $"{Parent.name}/");
				Parent = Parent.parent;
			}
			return TargetPath.ToString();
		}
		
		public static AnimationClip GetAnimationClip(UnityEngine.Object Target,string TargetPath, bool Boolen = true)
		{
			var clip = new AnimationClip();
			var KeyValue = 0f;
			var Key = new Keyframe(0f,KeyValue);
			var Curve = new AnimationCurve(Key);
			//GameObjectアニメーション
			KeyValue = Convert.ToInt16(Target.GetType() == typeof(GameObject) ? Boolen
				: Target is Component ? Boolen
				: Boolen);
			Key.value = KeyValue;
			Curve.ClearKeys();
			Curve.AddKey(Key);
			clip.SetCurve(TargetPath,
				Target.GetType(),
				Target.GetType() == typeof(GameObject) ? $"m_IsActive"
				: Target is Component ? $"m_Enabled"
				: "",
				Curve);
			return clip;
		}
	}
}
