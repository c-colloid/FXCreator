using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.Animations;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_Constraint : ToggleClip
	{
		const string m_componentMenuItemPath = ToggleClip.m_componentMenuItemPath + "Constraint/";
		const string m_aimConstraintMenuItem = m_componentMenuItemPath + nameof(AimConstraint);
		const string m_lookAtConstraintMenuItem = m_componentMenuItemPath + nameof(LookAtConstraint);
		const string m_parentConstraintMenuItem = m_componentMenuItemPath + nameof(ParentConstraint);
		const string m_positionConstraintMenuItem = m_componentMenuItemPath + nameof(PositionConstraint);
		const string m_rotationConstraintMenuItem = m_componentMenuItemPath + nameof(RotationConstraint);
		const string m_scaleConstraintMenuItem = m_componentMenuItemPath + nameof(ScaleConstraint);
		
		const string m_aimConstraintContextMenuItem = "CONTEXT/" + nameof(AimConstraint) +"/"+ m_menuItem;

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_aimConstraintMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_aimConstraintContextMenuItem)]
		static bool VaridationGenerateAimConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<AimConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_aimConstraintMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_aimConstraintContextMenuItem)]
		static void GenerateAimConstraintClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<AimConstraint>())));
		}
		
		const string m_LookAtConstraintContextMenuItem = "CONTEXT/" + nameof(LookAtConstraint) +"/"+ m_menuItem;

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_lookAtConstraintMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_LookAtConstraintContextMenuItem)]
		static bool VaridationGenerateLookAtConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<LookAtConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_lookAtConstraintMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_LookAtConstraintContextMenuItem)]
		static void GenerateLookAtConstraintClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<LookAtConstraint>())));
		}
		
		const string m_ParentConstraintContextMenuItem = "CONTEXT/" + nameof(ParentConstraint) +"/"+ m_menuItem;

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_parentConstraintMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_ParentConstraintContextMenuItem)]
		static bool VaridationGenerateParentConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<ParentConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_parentConstraintMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_ParentConstraintContextMenuItem)]
		static void GenerateParentConstraintClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<ParentConstraint>())));
		}
		
		const string m_PositionConstraintContextMenuItem = "CONTEXT/" + nameof(PositionConstraint) +"/"+ m_menuItem;

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_positionConstraintMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_PositionConstraintContextMenuItem)]
		static bool VaridationGeneratePositionConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<PositionConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_positionConstraintMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_PositionConstraintContextMenuItem)]
		static void GeneratePositionConstraintClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<PositionConstraint>())));
		}
		
		const string m_RotationConstraintContextMenuItem = "CONTEXT/" + nameof(RotationConstraint) +"/"+ m_menuItem;

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_rotationConstraintMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_RotationConstraintContextMenuItem)]
		static bool VaridationGenerateRotationConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<RotationConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_rotationConstraintMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_RotationConstraintContextMenuItem)]
		static void GenerateRotationConstraintClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<RotationConstraint>())));
		}
		
		const string m_ScaleConstraintContextMenuItem = "CONTEXT/" + nameof(ScaleConstraint) +"/"+ m_menuItem;

		[MenuItem(m_menuItem ,validate = true ,menuItem = m_scaleConstraintMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_ScaleConstraintContextMenuItem)]
		static bool VaridationGenerateScaleConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<ScaleConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_scaleConstraintMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_ScaleConstraintContextMenuItem)]
		static void GenerateScaleConstraintClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<ScaleConstraint>())));
		}
	}	
}
