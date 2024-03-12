using UnityEditor;
using UnityEngine;

namespace colloid.FXCreater
{
	class TransformAndQuaternion
	{
		public Vector3 vec;
		public Quaternion quaternion;

		public TransformAndQuaternion(Vector3 vector, Quaternion rot)
		{
			vec = vector;
			quaternion = rot;
		}
	}
}
