using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using colloid.FXCreater.Utility;
using CustomUI;

namespace colloid.FXCreater
{
	public class RenderEvents
	{
		static DropDownField clipsDropdown;
		static ToggleinButton playButton;
		static Slider playSlider;
		
		//アニメーションプレビュー関連
	#region AnimationGraph
		static public List<AnimationClip> clips;
		static bool m_playing = false;
		static PlayableGraph graph;
		static PlayableGraph defaultGraph;
		static PlayableGraph defaultHumanoidGraph;
		static AnimationClipPlayable clipPlayable;
	#endregion
		
		public static void SetupEvents(VisualElement root)
		{
			// レンダー関連のイベントハンドラの設定
			// ...
		}
		
		public static void SetDropDownText(string path){
			clipsDropdown.Popupvalues.Clear();
			clips.Clear();
			clipsDropdown.Popupvalues.Add("-select-");
			if (string.IsNullOrEmpty(path)) return;
        	
			clips = AnimationClipsUtility.GetClips(path);
			foreach (var clip in clips) clipsDropdown.Popupvalues.Add(AssetDatabase.GetAssetPath(clip).Replace($"{FolderPathEvents.FolderPathTextField.text}/",""));
		}
	}
}
