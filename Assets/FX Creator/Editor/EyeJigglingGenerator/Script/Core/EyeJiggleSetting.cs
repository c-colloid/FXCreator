using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace colloid.FXCreator.EyeJigglingGenerator
{
	
public class EyeJiggleSetting : MonoBehaviour
#if VRC
,VRC.SDKBase.IEditorOnly
#endif

{
	[SerializeField]
	SkinnedMeshRenderer m_mesh;
	[SerializeField,Range(0,100)]
	int m_layerIndex = 1;
	[SerializeField]
	string m_mainBlendShape = "-none-";
	[SerializeField]
	AnimationCurve m_animationCurve
		= new AnimationCurve(new Keyframe[]{new Keyframe(0,0),new Keyframe(0.5f,1),new Keyframe(1.0f,0),new Keyframe(2,0)});
	[SerializeField][HideInInspector]
	Vector2 m_timerClamp;
	[SerializeField]
	BlendAnimation[] m_blendAnimations = new BlendAnimation[1];
	[SerializeField][HideInInspector]
	Vector2 m_timerClamps;
	
	public SkinnedMeshRenderer Mesh => m_mesh;
	public int LayerIndex {get => m_layerIndex; set => m_layerIndex = value;}
	public string MainBlendShape => m_mainBlendShape;
	public AnimationCurve AnimationCurve => m_animationCurve;
	public Vector2 TimerClamp => m_timerClamp;
	public BlendAnimation[] BlendAnimations {
		get => m_blendAnimations;
		set => m_blendAnimations = value;
	}
	public Vector2 TimerClamps {get => m_timerClamps; set => m_timerClamps = value;}
	
	public string Path {get;set;}
	
	#region PublicMethod
	public AnimationClip GenerateClip()
	{
		var SaveBlendANimations = new List<BlendAnimation>(m_blendAnimations);
		SaveBlendANimations.Add(new BlendAnimation(){BlendShape = m_mainBlendShape, AnimationCurve = m_animationCurve});
		return GenerateAnimClip.Generate(SaveBlendANimations,m_mesh);
	}
	#endregion
	
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

[System.Serializable]
public class BlendAnimation
{
	[SerializeField]
	string m_blendshape = "-none-";
	[SerializeField]
	AnimationCurve m_animationcurve;
	[SerializeField][HideInInspector]
	Vector2 m_timerClamp;
	[SerializeField]
	bool m_playbutton;
	
	public string BlendShape {get => m_blendshape; set => m_blendshape = value;}
	public AnimationCurve AnimationCurve {get => m_animationcurve; set => m_animationcurve = value;}
	public Vector2 TimerClamp {get => m_timerClamp; set => m_timerClamp = value;}
	public bool PlayButton {get => m_playbutton; set => m_playbutton = value;}
}
}