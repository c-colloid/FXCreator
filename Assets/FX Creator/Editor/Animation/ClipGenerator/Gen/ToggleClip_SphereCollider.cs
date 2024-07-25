using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_SphereCollider : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + nameof(SphereCollider);
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		static bool VaridationGenerateSphereColliderClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<SphereCollider>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		static void GenerateSphereColliderClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<SphereCollider>()));
		}
	}
}