using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_AudioSource : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + nameof(AudioSource);
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		static bool VaridationGenerateAudioSourceClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<AudioSource>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		static void GenerateAudioSourceClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<AudioSource>()));
		}
	}
}