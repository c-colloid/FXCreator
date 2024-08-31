using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_LineRenderer : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + "Renderer/" + nameof(LineRenderer);
		const string m_contextMenuItem = "CONTEXT/" + nameof(LineRenderer) +"/"+ m_menuItem;
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_contextMenuItem)]
		static bool VaridationGenerateLineRendererClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<LineRenderer>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_contextMenuItem)]
		static void GenerateLineRendererClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<LineRenderer>())));
		}
	}
}