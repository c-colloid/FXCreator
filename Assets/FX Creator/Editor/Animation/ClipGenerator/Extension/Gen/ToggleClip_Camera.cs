using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_Camera : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + "" + nameof(Camera);
		const string m_contextMenuItem = "CONTEXT/" + nameof(Camera) +"/"+ m_menuItem;
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_contextMenuItem)]
		static bool VaridationGenerateCameraClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<Camera>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_contextMenuItem)]
		static void GenerateCameraClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<Camera>())));
		}
	}
}