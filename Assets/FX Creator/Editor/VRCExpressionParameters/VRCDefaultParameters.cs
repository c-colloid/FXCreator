using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using VRC.SDK3.Avatars.ScriptableObjects;
using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;

namespace colloid.FXCreator.VRCExpressionParametersExtention
{

static public class VRCDefaultParameters
{
	static readonly List<Parameter> m_VRCParameters = new List<Parameter>(){
		new Parameter(){name = "IsLocal",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "Viseme",valueType = VRCExpressionParameters.ValueType.Int,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "Voice",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "GestureLeft",valueType = VRCExpressionParameters.ValueType.Int,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "GestureRight",valueType = VRCExpressionParameters.ValueType.Int,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "GestureLeftWeight",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "GestureRightWeight",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "AngularY",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "VelocityX",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "VelocityY",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "VelocityZ",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "VelocityMagnitude",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "Upright",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "Grounded",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "Seated",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "AFK",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "TrackingType",valueType = VRCExpressionParameters.ValueType.Int,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "VRMode",valueType = VRCExpressionParameters.ValueType.Int,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "MuteSelf",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "InStation",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "Earmuffs",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "IsOnFriendsList",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "AvatarVersion",valueType = VRCExpressionParameters.ValueType.Int,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "ScaleModified",valueType = VRCExpressionParameters.ValueType.Bool,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "ScaleFactor",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "ScaleFactorInverse",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "EyeHeightAsMeters",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false},
		new Parameter(){name = "EyeHeightAsPercent",valueType = VRCExpressionParameters.ValueType.Float,saved = false,defaultValue = 0,networkSynced = false}
	};
	
	static public List<Parameter> VRCParameters => m_VRCParameters;
}
}