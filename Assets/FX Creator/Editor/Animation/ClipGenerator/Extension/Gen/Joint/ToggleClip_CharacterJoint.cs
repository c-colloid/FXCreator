using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_CharacterJoint : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + "Joint/" + nameof(CharacterJoint);
		const string m_contextMenuItem = "CONTEXT/" + nameof(CharacterJoint) +"/"+ m_menuItem;
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_contextMenuItem)]
		static bool VaridationGenerateCharacterJointClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<CharacterJoint>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_contextMenuItem)]
		static void GenerateCharacterJointClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<CharacterJoint>())));
		}
	}
}