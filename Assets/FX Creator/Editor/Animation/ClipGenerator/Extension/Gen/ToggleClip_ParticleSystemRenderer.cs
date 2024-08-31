using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public class ToggleClip_ParticleSystemRenderer : ToggleClip
	{
		const string m_componentMenuItem = m_componentMenuItemPath + "" + nameof(ParticleSystemRenderer);
		const string m_contextMenuItem = "CONTEXT/" + nameof(ParticleSystem) + "/" + nameof(ParticleSystemRenderer) +"/"+ m_menuItem;
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_contextMenuItem)]
		static bool VaridationGenerateParticleSystemClip()
		{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<ParticleSystemRenderer>(out var result));
		}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_contextMenuItem)]
		static void GenerateParticleSystemClip()
		{
			OnceFilter(() =>
				GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<ParticleSystemRenderer>())));
		}
	}
}