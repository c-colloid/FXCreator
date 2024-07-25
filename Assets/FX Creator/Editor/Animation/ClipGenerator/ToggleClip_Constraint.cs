using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.Animations;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_Constraint : ToggleClip
	{
		const string m_aimConstraintMenuItem = m_componentMenuItemPath + nameof(AimConstraint);
		const string m_lookAtConstraintMenuItem = m_componentMenuItemPath + nameof(LookAtConstraint);
		const string m_parentConstraintMenuItem = m_componentMenuItemPath + nameof(ParentConstraint);
		const string m_positionConstraintMenuItem = m_componentMenuItemPath + nameof(PositionConstraint);
		const string m_rotationConstraintMenuItem = m_componentMenuItemPath + nameof(RotationConstraint);
		const string m_scaleConstraintMenuItem = m_componentMenuItemPath + nameof(ScaleConstraint);
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_aimConstraintMenuItem)]
		static bool ValidateGenerateAimConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<AimConstraint>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_aimConstraintMenuItem, priority = 1011)]
		static void GenerateAimConstraintClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<AimConstraint>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_lookAtConstraintMenuItem)]
		static bool ValidateGenerateLookAtConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<LookAtConstraint>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_lookAtConstraintMenuItem, priority = 1011)]
		static void GenerateLookAtConstraintClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<LookAtConstraint>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_parentConstraintMenuItem)]
		static bool ValidateGenerateParentConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<ParentConstraint>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_parentConstraintMenuItem, priority = 1011)]
		static void GenerateParentConstraintClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<ParentConstraint>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_positionConstraintMenuItem)]
		static bool VaridationGeneratePositionConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<PositionConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_positionConstraintMenuItem, priority = 1011)]
		static void GeneratePositionConstraintClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<PositionConstraint>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_rotationConstraintMenuItem)]
		static bool VaridationGenerateRotationConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<RotationConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_rotationConstraintMenuItem, priority = 1011)]
		static void GenerateRotationConstraintClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<RotationConstraint>()));
		}
		
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_scaleConstraintMenuItem)]
		static bool VaridationGenerateScaleConstraintClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<ScaleConstraint>(out var result));
		}

		[MenuItem(m_menuItem ,menuItem = m_scaleConstraintMenuItem, priority = 1011)]
		static void GenerateScaleConstraintClip()
		{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<ScaleConstraint>()));
		}
	}	
}
