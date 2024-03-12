using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace colloid.FXCreater
{
	public class PoseToHumanoidAnimation : IDisposable
	{
		public void Dispose()
		{
		
		}
	
		static void AddEditorCurve(AnimationClip animationClip_0, List<AnimationCurveStruct> curveStructs)
		{
			animationClip_0.ClearCurves();
			foreach (var curve in curveStructs)
			{
				AnimationUtility.SetEditorCurve(animationClip_0, curve.binding, curve.curve);
			}
		}
		
		public static float MultiplyWithAbsOrMultiply(float value, float factor, float alternativeFactor)
		{
			if (value < 0f)
			{
				return value * Mathf.Abs(factor);
			}
			return value * alternativeFactor;
		}

		public static float DivideWithAbsOrDivide(float value, float divisor, float alternativeDivisor)
		{
			if (value < 0f)
			{
				return value / Mathf.Abs(divisor);
			}
			return value / alternativeDivisor;
		}
		
		public static float ComplexOperation(float value1, float value2, float factor, float alternativeFactor)
		{
			float num = MultiplyWithAbsOrMultiply(value1, factor, alternativeFactor);
			float num2 = MultiplyWithAbsOrMultiply(value2, factor, alternativeFactor);
			num2 = num + Mathf.DeltaAngle(num, num2);
			return DivideWithAbsOrDivide(num2, factor, alternativeFactor);
		}
		
		private static void AddListMuscleCurves(List<AnimationCurveStruct> curveList, float[] muscles, float time)
		{
			string[] AnimParameterStrs = CreateAnimationUtility.GetMuscleNameArray();

			for (int i = 0; i < HumanTrait.MuscleCount; i++)
			{
				float value = ComplexOperation(0.0f, muscles[i], HumanTrait.GetMuscleDefaultMin(i), HumanTrait.GetMuscleDefaultMax(i));

				AnimationCurve curve = new AnimationCurve();
				curve.AddKey(time, value);

				AnimationCurveStruct item = default(AnimationCurveStruct);
				item.binding = CreateCurveBinding(AnimParameterStrs[i], -1);
				item.curve = curve;
				CreateAnimationUtility.SetTangentMode(item.curve);

				curveList.Add(item);
			}
		}
		
		private static void AddListAnimationCurve(List<AnimationCurveStruct> curveList, string IKTarget, AnimationCurve[] curve)
		{
			for (int j = 0; j < curve.Length; j++)
			{
				curveList.Add(new AnimationCurveStruct
				{
					binding = CreateCurveBinding(IKTarget, j),
					curve = curve[j]
				});
			}
		}
		
		private static EditorCurveBinding CreateCurveBinding(string property, int xyzwNum)
		{
			string[] xyzwStr = new string[] { ".x", ".y", ".z", ".w" };

			EditorCurveBinding result = default(EditorCurveBinding);
			result.path = "";
			if (xyzwNum >= 0)
			{
				result.propertyName = property + xyzwStr[xyzwNum];
			}
			else
			{
				result.propertyName = property;
			}

			result.type = typeof(Animator);

			return result;
		}
		
		public static void ConvertPose(AnimationClip anim, Animator animator, float time = 0.0f)
		{
			if (!animator.isHuman)
				return;

			HumanPose pose = new HumanPose();
			HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform.root);

			AnimationCurve[] rootTCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(3);
			AnimationCurve[] rootQCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(4);
			AnimationCurve[] LeftHandTCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(3);
			AnimationCurve[] LeftHandQCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(4);
			AnimationCurve[] RightHandTCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(3);
			AnimationCurve[] RightHandQCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(4);
			AnimationCurve[] LeftFootTCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(3);
			AnimationCurve[] LeftFootQCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(4);
			AnimationCurve[] RightFootTCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(3);
			AnimationCurve[] RightFootQCurveArray = CreateAnimationUtility.CreateAnimationCurveArray(4);

			List<AnimationCurveStruct> curveList = new List<AnimationCurveStruct>();

			handler.GetHumanPose(ref pose);

			Vector3 bodyPosition = pose.bodyPosition - animator.transform.position;

			Quaternion bodyRotation = pose.bodyRotation;

			CreateAnimationUtility.SetAnimationCurve(rootTCurveArray, time, bodyPosition);
			CreateAnimationUtility.SetAnimationCurve(rootQCurveArray, time, bodyRotation);

			Transform leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
			Transform rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);

			Transform leftFootBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
			Transform rightFootBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);

			TransformAndQuaternion leftHandPose = new TransformAndQuaternion(leftHandBone.position, leftHandBone.rotation);
			TransformAndQuaternion rightHandPose = new TransformAndQuaternion(rightHandBone.position, rightHandBone.rotation);

			TransformAndQuaternion leftFootPose = new TransformAndQuaternion(leftFootBone.position, leftFootBone.rotation);
			TransformAndQuaternion rightFootPose = new TransformAndQuaternion(rightFootBone.position, rightFootBone.rotation);

			var _avatar_transform = animator.transform;

			leftHandPose.vec = _avatar_transform.InverseTransformPoint(leftHandPose.vec);
			rightHandPose.vec = _avatar_transform.InverseTransformPoint(rightHandPose.vec);

			leftFootPose.vec = _avatar_transform.InverseTransformPoint(leftFootPose.vec);
			rightFootPose.vec = _avatar_transform.InverseTransformPoint(rightFootPose.vec);

			Quaternion inverseRotation = Quaternion.Inverse(_avatar_transform.rotation);

			leftHandPose.quaternion = inverseRotation * leftHandPose.quaternion;
			rightHandPose.quaternion = inverseRotation * rightHandPose.quaternion;

			leftFootPose.quaternion = inverseRotation * leftFootPose.quaternion;
			rightFootPose.quaternion = inverseRotation * rightFootPose.quaternion;

			var destHumanScale = HumanoidPoseUtility.GetHumanScale(animator);

			TransformAndQuaternion rootPose = new TransformAndQuaternion(bodyPosition * destHumanScale, bodyRotation);

			TransformAndQuaternion leftHand_IKPose = HumanoidPoseUtility.GetPoseFromRootPose(animator.avatar, destHumanScale, AvatarIKGoal.LeftHand, rootPose, leftHandPose);
			TransformAndQuaternion rightHand_IKPose = HumanoidPoseUtility.GetPoseFromRootPose(animator.avatar, destHumanScale, AvatarIKGoal.RightHand, rootPose, rightHandPose);
			TransformAndQuaternion leftFoot_IKPose = HumanoidPoseUtility.GetPoseFromRootPose(animator.avatar, destHumanScale, AvatarIKGoal.LeftFoot, rootPose, leftFootPose);
			TransformAndQuaternion rightFoot_IKPose = HumanoidPoseUtility.GetPoseFromRootPose(animator.avatar, destHumanScale, AvatarIKGoal.RightFoot, rootPose, rightFootPose);

			CreateAnimationUtility.SetAnimationCurve(LeftHandTCurveArray, time, leftHand_IKPose.vec);
			CreateAnimationUtility.SetAnimationCurve(LeftHandQCurveArray, time, leftHand_IKPose.quaternion);
			CreateAnimationUtility.SetAnimationCurve(RightHandTCurveArray, time, rightHand_IKPose.vec);
			CreateAnimationUtility.SetAnimationCurve(RightHandQCurveArray, time, rightHand_IKPose.quaternion);

			CreateAnimationUtility.SetAnimationCurve(LeftFootTCurveArray, time, leftFoot_IKPose.vec);
			CreateAnimationUtility.SetAnimationCurve(LeftFootQCurveArray, time, leftFoot_IKPose.quaternion);
			CreateAnimationUtility.SetAnimationCurve(RightFootTCurveArray, time, rightFoot_IKPose.vec);
			CreateAnimationUtility.SetAnimationCurve(RightFootQCurveArray, time, rightFoot_IKPose.quaternion);

			AddListMuscleCurves(curveList, pose.muscles, time);

			AddListAnimationCurve(curveList, "RootT", rootTCurveArray);
			AddListAnimationCurve(curveList, "RootQ", rootQCurveArray);

			AddListAnimationCurve(curveList, "LeftHandT", LeftHandTCurveArray);
			AddListAnimationCurve(curveList, "LeftHandQ", LeftHandQCurveArray);
			AddListAnimationCurve(curveList, "RightHandT", RightHandTCurveArray);
			AddListAnimationCurve(curveList, "RightHandQ", RightHandQCurveArray);

			AddListAnimationCurve(curveList, "LeftFootT", LeftFootTCurveArray);
			AddListAnimationCurve(curveList, "LeftFootQ", LeftFootQCurveArray);
			AddListAnimationCurve(curveList, "RightFootT", RightFootTCurveArray);
			AddListAnimationCurve(curveList, "RightFootQ", RightFootQCurveArray);

			AddEditorCurve(anim, curveList);
		}
	}	
}
