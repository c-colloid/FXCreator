using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_BoxCollider : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + nameof(BoxCollider);
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		static bool VaridationGenerateBoxColliderClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<BoxCollider>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		static void GenerateBoxColliderClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<BoxCollider>()));
		}
	}
}