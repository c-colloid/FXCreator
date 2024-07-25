using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_CapsuleCollider : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + nameof(CapsuleCollider);
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		static bool VaridationGenerateCapsuleColliderClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<CapsuleCollider>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		static void GenerateCapsuleColliderClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<CapsuleCollider>()));
		}
	}
}