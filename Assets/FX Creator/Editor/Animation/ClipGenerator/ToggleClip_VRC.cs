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
		const string m_VRCPhysBoneMenuItem = m_componentMenuItemPath + "VRChat/" + nameof(VRCPhysBone);
		const string m_VRCPhysBoneContextMenuItem = "CONTEXT/" + nameof(VRCPhysBone) +"/"+ m_menuItem;
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_VRCPhysBoneMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_VRCPhysBoneContextMenuItem)]
		static bool VaridationGenerateVRCPhysBoneClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<VRCPhysBone>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_VRCPhysBoneMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_VRCPhysBoneContextMenuItem)]
		static void GenerateVRCPhysBoneClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<VRCPhysBone>()));
		}
		
		const string m_VRCPhysBoneColliderMenuItem = m_componentMenuItemPath + "VRChat/" + nameof(VRCPhysBoneCollider);
		const string m_VRCPhysBoneColliderContextMenuItem = "CONTEXT/" + nameof(VRCPhysBoneCollider) +"/"+ m_menuItem;
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_VRCPhysBoneColliderMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_VRCPhysBoneColliderContextMenuItem)]
		static bool VaridationGenerateVRCPhysBoneColliderClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<VRCPhysBoneCollider>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_VRCPhysBoneColliderMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_VRCPhysBoneColliderContextMenuItem)]
		static void GenerateVRCPhysBoneColliderClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<VRCPhysBoneCollider>()));
		}
	}
}
#endif