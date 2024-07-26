using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_MeshCollider : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + "Collider/" + nameof(MeshCollider);
		const string m_contextMenuItem = "CONTEXT/" + nameof(MeshCollider) +"/"+ m_menuItem;
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_contextMenuItem)]
		static bool VaridationGenerateMeshColliderClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<MeshCollider>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_contextMenuItem)]
		static void GenerateMeshColliderClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<MeshCollider>()));
		}
	}
}