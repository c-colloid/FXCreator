#if VRC
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_VRC : ToggleClip
	{
		const string m_physBoneMenuItem = m_componentMenuItemPath + nameof(VRCPhysBone);
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_physBoneMenuItem)]
		static bool VaridationGeneratePhysBoneClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<VRCPhysBone>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_physBoneMenuItem, priority = 1011)]
		static void GeneratePhysBoneClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<VRCPhysBone>()));
		}
		
		const string m_physBoneColliderMenuItem = m_componentMenuItemPath + nameof(VRCPhysBoneCollider);

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_physBoneColliderMenuItem)]
		static bool VaridationGenerateVRCPhysBoneColliderClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<VRCPhysBoneCollider>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_physBoneColliderMenuItem, priority = 1011)]
		static void GenerateVRCPhysBoneColliderClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<VRCPhysBoneCollider>()));
		}
	}
}
#endif