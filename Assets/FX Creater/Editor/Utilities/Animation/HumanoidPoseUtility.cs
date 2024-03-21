using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace colloid.FXCreater
{
	class HumanoidPoseUtility
	{
		public static float GetHumanScale(Animator animator)
		{
			float humanScale = animator.humanScale;
			if (animator != null && !animator.isInitialized)
			{
				animator.Rebind();
			}
			return humanScale;
		}
		
		public static TransformAndQuaternion GetPoseFromRootPose(Avatar avatar_0, float float_0, AvatarIKGoal avatarIKGoal_0, TransformAndQuaternion sourcePose, TransformAndQuaternion targetPose)
		{
			int num = (int)GetHumanBoneFromIKGoal(avatarIKGoal_0);

			Quaternion rhs = GetPostRotation(avatar_0, num);
			TransformAndQuaternion result = new TransformAndQuaternion(targetPose.vec, targetPose.quaternion * rhs);
			if (avatarIKGoal_0 == AvatarIKGoal.LeftFoot || avatarIKGoal_0 == AvatarIKGoal.RightFoot)
			{
				float x = GetAxisLength(avatar_0, num);
				Vector3 point = new Vector3(x, 0f, 0f);
				result.vec += result.quaternion * point;
			}
			Quaternion quaternion = Quaternion.Inverse(sourcePose.quaternion);
			result.vec = quaternion * (result.vec - sourcePose.vec);
			result.quaternion = quaternion * result.quaternion;
			result.vec /= float_0;
			
			return result;
		}
		
		//public static TransformAndQuaternion GetPoseFromRootPose(Avatar avatar_0, float float_0, AvatarIKGoal avatarIKGoal_0, TransformAndQuaternion sourcePose, TransformAndQuaternion targetPose)
		//{
		//	AnimationStream stream = new AnimationStream();
		//	var humanstream = stream.AsHuman();
			
		//	var result = new TransformAndQuaternion(humanstream.GetGoalPositionFromPose(avatarIKGoal_0),humanstream.GetGoalRotationFromPose(avatarIKGoal_0));
		//	return result;
		//}
		
		public static HumanBodyBones GetHumanBoneFromIKGoal(AvatarIKGoal avatarIKGoal_0)
		{
			HumanBodyBones result = HumanBodyBones.LastBone;
			switch (avatarIKGoal_0)
			{
			case AvatarIKGoal.LeftFoot:
				result = HumanBodyBones.LeftFoot;
				break;
			case AvatarIKGoal.RightFoot:
				result = HumanBodyBones.RightFoot;
				break;
			case AvatarIKGoal.LeftHand:
				result = HumanBodyBones.LeftHand;
				break;
			case AvatarIKGoal.RightHand:
				result = HumanBodyBones.RightHand;
				break;
			}
			return result;
		}
		
		static MethodInfo _GetPostRotation, _GetAxisLength;

		public static Quaternion GetPostRotation(Avatar avatar_0, int int_0)
		{
			if (_GetPostRotation == null)
			{
				_GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			return (Quaternion)_GetPostRotation.Invoke(avatar_0, new object[]
			{
				int_0
			});
		}

		public static float GetAxisLength(Avatar avatar_0, int int_0)
		{
			if (_GetAxisLength == null)
			{
				_GetAxisLength = typeof(Avatar).GetMethod("GetAxisLength", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			return (float)_GetAxisLength.Invoke(avatar_0, new object[]
			{
				int_0
			});
		}
	}
}
