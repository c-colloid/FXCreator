using UnityEditor;
using UnityEngine;

namespace colloid.FXCreater
{
	class CreateAnimationUtility
	{
		public static AnimationCurve[] CreateAnimationCurveArray(int count)
		{
			AnimationCurve[] array = new AnimationCurve[count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new AnimationCurve();
			}
			return array;
		}
		
		public static void SetAnimationCurve(AnimationCurve[] animationCurve_0, float time, Vector3 positionValue)
		{
			for (int i = 0; i < animationCurve_0.Length; i++)
			{
				animationCurve_0[i].AddKey(time, positionValue[i]);
			}
		}
		
		public static void SetAnimationCurve(AnimationCurve[] animationCurve_0, float time, Quaternion rotationValue)
		{
			for (int i = 0; i < animationCurve_0.Length; i++)
			{
				animationCurve_0[i].AddKey(time, rotationValue[i]);
			}
		}
		
		public static string[] GetMuscleNameArray()
		{
			var AnimParameters = new string[HumanTrait.MuscleCount];

			for (int i = 0; i < AnimParameters.Length; i++)
			{
				AnimParameters[i] = ConvertToAnimParameter(HumanTrait.MuscleName[i]);
			}
			return AnimParameters;
		}
		
		private static string ConvertToAnimParameter(string muscleName)
		{
			if (muscleName.EndsWith("Stretched"))
			{
				string[] characters = muscleName.Split(new char[] { ' ' });
				muscleName = string.Concat(new string[] { characters[0], "Hand.", characters[1], ".", characters[2], " ", characters[3] });
			}

			if (muscleName.EndsWith("Spread"))
			{
				string[] characters = muscleName.Split(new char[] { ' ' });
				muscleName = string.Concat(new string[] { characters[0], "Hand.", characters[1], ".", characters[2] });
			}
			return muscleName;
		}
		
		public static void SetTangentMode(AnimationCurve targetCurve)
		{
			for (int i = 0; i < targetCurve.length; i++)
			{
				AnimationUtility.SetKeyBroken(targetCurve, i, true);
				AnimationUtility.SetKeyLeftTangentMode(targetCurve, i, AnimationUtility.TangentMode.ClampedAuto);
				AnimationUtility.SetKeyRightTangentMode(targetCurve, i, AnimationUtility.TangentMode.ClampedAuto);
			}
		}
	}
}
