using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
using colloid.FXCreator.Animation.Utility;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip
	{
		const string m_menuItem = "ClipGen";
		const string m_skinnedMeshRendererMenuItem = "GameObject/" + m_menuItem + "/Compornent/" + nameof(SkinnedMeshRenderer);
		const string m_meshRendererMenuItem = "GameObject/" + m_menuItem + "/Compornent/" + nameof(MeshRenderer);
		const string m_saveDialogTitle = "Save AnimationClip";
		const string m_saveDialogMessage = "Please select save folder.";
		
		static string m_saveFolderPath = "Assets/";
		
		static bool m_settingsSetPrefix = true;
		const string m_settingsSetPrefixMenuItem = "Tools/" + m_menuItem + "/Settings/ClipName/SetPrefix-(ClipGen)";
		static bool m_settingsSetSuffix = true;
		const string m_settingsSetSuffixMenuItem = "Tools/" + m_menuItem + "/Settings/ClipName/SetSuffix";
		static bool m_init = false;
		
		[MenuItem(m_menuItem ,menuItem = "GameObject/" + m_menuItem + "/GameObject", priority = 1000)]
		static void GenerateGameObjectClip()
		{
			GenerateToggelGameObjectsClip(Selection.gameObjects);
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_skinnedMeshRendererMenuItem)]
		static bool ValidateGenerateSkinnedMeshRendererClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<SkinnedMeshRenderer>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_skinnedMeshRendererMenuItem, priority = 1011)]
		static void GenerateSkinnedMeshRendererClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<SkinnedMeshRenderer>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_meshRendererMenuItem)]
		static bool ValidateGenerateMeshRendererClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<MeshRenderer>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_meshRendererMenuItem, priority = 1011)]
		static void GenerateMeshRendererClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<MeshRenderer>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_settingsSetPrefixMenuItem)]
		static bool ValidateSettingsSetPrefix()
		{
			//初期値の設定
			Menu.SetChecked(m_settingsSetPrefixMenuItem,m_settingsSetPrefix);
			return true;
		}
		
		[MenuItem(m_menuItem ,menuItem = m_settingsSetPrefixMenuItem)]
		static void SettingsSetPrefix()
		{
			m_settingsSetPrefix = !m_settingsSetPrefix;
			Menu.SetChecked(m_settingsSetPrefixMenuItem,m_settingsSetPrefix);
			EditorUserSettings.SetConfigValue(m_settingsSetPrefixMenuItem,m_settingsSetPrefix.ToString());
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_settingsSetSuffixMenuItem)]
		static bool ValidateSettingsSetSuffix()
		{
			//初期値の設定
			Menu.SetChecked(m_settingsSetSuffixMenuItem,m_settingsSetSuffix);
			return true;
		}
		
		[MenuItem(m_menuItem ,menuItem = m_settingsSetSuffixMenuItem)]
		static void SettingsSetSuffix()
		{
			m_settingsSetSuffix = !m_settingsSetSuffix;
			Menu.SetChecked(m_settingsSetSuffixMenuItem,m_settingsSetSuffix);
			EditorUserSettings.SetConfigValue(m_settingsSetSuffixMenuItem,m_settingsSetSuffix.ToString());
		}
		
		[MenuItem("Tools/"+m_menuItem +"/Init" ,validate = true)][MenuItem("GameObject/"+m_menuItem+"/Init" ,validate = true)]
		static bool ValidateInitSettings()
		{
			Init();
			return false;
		}
		[MenuItem("Tools/"+m_menuItem+"/Init")][MenuItem("GameObject/"+m_menuItem+"/Init")]
		static void InitSettings()
		{
			
		}
		
		static void Init()
		{
			if (m_init) return;
			var val = EditorUserSettings.GetConfigValue(m_settingsSetPrefixMenuItem);
			if (!string.IsNullOrEmpty(val))
			{
				m_settingsSetPrefix = val.Equals(true.ToString(),StringComparison.Ordinal);
			}
			
			val = EditorUserSettings.GetConfigValue(m_settingsSetSuffixMenuItem);
			if (!string.IsNullOrEmpty(val))
			{
				m_settingsSetSuffix = val.Equals(true.ToString(),StringComparison.Ordinal);
			}
			
			m_init = true;
		}
		
		static void GenerateToggelGameObjectsClip(GameObject[] selections)
		{
			selections.ToList().ForEach(o => GenerateToggleGameObjectClip(o));
		}
		
		static void GenerateToggleGameObjectClip(GameObject GO)
		{
			GenClips(GO);
			
			//Clip保存(テスト用)
			//AssetDatabase.CreateAsset(clip,$"Assets/{GO.name}_{m_menuItem}.anim");
		}
		
		static void GenerateToggelActiveComponentsClip(Component[] selections)
		{
			selections.ToList().ForEach(o => GenerateToggleActiveComponentClip(o));
		}
		
		static void GenerateToggelActiveComponentsClip(IEnumerable<Component> selections)
		{
			selections.ToList().ForEach(o => GenerateToggleActiveComponentClip(o));
		}
		
		static void GenerateToggleActiveComponentClip(Component Target)
		{
			GenClips(Target);
			
			//Clip保存(テスト用)
			//AssetDatabase.CreateAsset(clip,$"Assets/{Target.name}_{m_menuItem}.anim");
		}
		
		
		static void GenClips(UnityEngine.Object Target)
		{
			var FileName = (m_settingsSetPrefix ? $"({m_menuItem})" : "")
				+ $"{Target.name}"
				+ (m_settingsSetSuffix ? $"-{Target.GetType().Name}-" : "");
			var FilePath = EditorUtility.SaveFilePanelInProject(m_saveDialogTitle,FileName,"anim",m_saveDialogMessage,m_saveFolderPath);
			if (String.IsNullOrEmpty(FilePath)) return;
			m_saveFolderPath = FilePath.Replace($"{FileName}.anim","");
			
			var TargetPath = GenerateClipUtility.GetTargetPathFromAvatar(Target is GameObject ? (Target as GameObject) : (Target as Component).gameObject);
			var clip_on = GenerateClipUtility.GetAnimationClip(Target,TargetPath,true);
			var clip_off = GenerateClipUtility.GetAnimationClip(Target,TargetPath,false);
			AssetDatabase.CreateAsset(clip_on,FilePath.Replace(".anim","_ON.anim"));
			AssetDatabase.CreateAsset(clip_off,FilePath.Replace(".anim","_OFF.anim"));
		}
	}	
}
