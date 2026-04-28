using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{
	public interface IToggleClip
	{
		public bool VaridationGenerateCharacterJointClip();

		public void GenerateCharacterJointClip();
	}
}