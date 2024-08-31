using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
//using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs;

namespace colloid.FXCreator.EyeJigglingGenerator
{
public class EyeJiggleInspectorPreview : IDisposable
{
	public Scene Scene{get; private set;}
	public RenderTexture RenderTexture{get; private set;}
	public Vector2Int RenderTextureSize{get; private set;}
	public Camera Camera{get; private set;} = null;
	bool m_didInitialize;
	float m_zoomFactor = 1.0f;
	float m_scaleFactor = 1.0f;
	Vector2 m_previewDir = new Vector2(0,180);
	Vector2 m_lightDir = Vector2.zero;
	Vector3 m_pivotPositionOffset = Vector3.zero;
	Quaternion m_meshRotationOffset = Quaternion.identity;
	Light m_light;
	
	Mesh m_target;
	public Mesh Mesh {get => m_target; set => m_target = value;}
	Material m_material;
	public Material Material{get => m_material; set => m_material = value;}
	Material[] m_materials;
	public class BakeBlendshape
	{
		internal int m_blendshape;
		internal float m_blendweight;
		
		public int BlendShapeIndex {get => m_blendshape; set => m_blendshape = value;}
		public float BlendShapeWeight {get => m_blendweight; set => m_blendweight = value;}
	}
	List<BakeBlendshape> m_bakeBlendshapes = new List<BakeBlendshape>();
	public List<BakeBlendshape> BakeBlendshapes{get => m_bakeBlendshapes; set => m_bakeBlendshapes = value;}
	public float BlendshapeWeight{get; set;}
	
	List<string> m_blendShapes = new List<string>();
	internal List<string> BlendShapes{get => m_blendShapes; private set => m_blendShapes = value;}
	internal int activeBlendshape = 0;
	
#region Constractor
	public EyeJiggleInspectorPreview()
	{
		try
		{
			Scene = EditorSceneManager.NewPreviewScene();
			
			var camera_GO = new GameObject("Preview Scene Camera", typeof(Camera));
			camera_GO.hideFlags = HideFlags.HideAndDontSave;
			AddGameObject(camera_GO);
			
			Camera = camera_GO.GetComponent<Camera>();
			Camera.cameraType = CameraType.Preview;
			Camera.fieldOfView = 30.0f;
			Camera.transform.position = Vector3.up*5;
			Camera.forceIntoRenderTexture = true;
			Camera.scene = Scene;
			Camera.enabled = false; //GameViewに影響を与えなくする
			
			var light_GO = new GameObject("Directional Light", typeof(Light));
			AddGameObject(light_GO);
			m_lightDir = new Vector2(-40,-40);
			light_GO.transform.rotation = Quaternion.Euler(m_lightDir.x, m_lightDir.y, 0);
			light_GO.hideFlags = HideFlags.HideAndDontSave;
			var light = light_GO.GetComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.1f;
			m_light = light;
			

			RenderSettings.ambientSkyColor = new Color(.1f,.1f,.1f,0);
			
			//Debug.Log("Initialized Preview");
			m_didInitialize = true;
		}
		catch (Exception e)
		{
			Debug.LogException(e);
			Dispose();
			m_didInitialize = false;
			throw e;
		}
		
	}
	
	public EyeJiggleInspectorPreview(Mesh target) : this()
	{
		m_target = target;
	}
	
	public EyeJiggleInspectorPreview(SkinnedMeshRenderer target) : this()
	{
		m_target = GameObject.Instantiate(target.sharedMesh);
		m_material = target.sharedMaterial;
		m_materials = target.sharedMaterials;
		
		var target_GO = target.gameObject;
		m_meshRotationOffset = target_GO.transform.rotation;
		// var vectors = m_target.vertices;
		
		// CopyGameObject(target_GO);

		// SetMeshRotation(m_target,target_GO.transform.rotation);
		CheckBlendshapes();
	}
#endregion

#region MeshSetting
	public static Mesh SetMeshRotation (Mesh mesh, Quaternion rotation)
	{
		var vectors = mesh.vertices;
		var data = GetBlendShapeVector3s(mesh);

		for(int i = 0; i < vectors.Length; i++)
		{
			vectors[i] = rotation * vectors[i];
		}
		Bounds bounds = new Bounds();
		bounds.size = rotation * mesh.bounds.size;
		bounds.center = rotation * mesh.bounds.center;
		mesh.bounds = bounds;


		for (int i = 0; i < data.Count; i++)
		{
			for (int index = 0; index < data[i].deltaVetices.Length; index++)
			{
				data[i].deltaVetices[index] = rotation * data[i].deltaVetices[index];
			}
		}

		mesh.vertices = vectors;
		mesh = mesh_copy(mesh, data);
		return mesh;
	}

	public static Mesh mesh_copy (Mesh mesh, List<BlendShapeVector3s> data = null) {
		Mesh m = new Mesh ();

		m.vertices = mesh.vertices;
		m.uv = mesh.uv;
		m.uv2 = mesh.uv2;
		m.uv3 = mesh.uv3;
		m.uv4 = mesh.uv4;
		m.triangles = mesh.triangles;

		m.bindposes = mesh.bindposes;
		m.boneWeights = mesh.boneWeights;
		m.bounds = mesh.bounds;
		m.colors = mesh.colors;
		m.colors32 = mesh.colors32;
		m.normals = mesh.normals;
		m.subMeshCount = mesh.subMeshCount;
		m.tangents = mesh.tangents;

		if (data != null)
		{
			m = blendshape_copy(mesh,data,m);
		}

		return m;
	}

	public class BlendShapeVector3s
	{
		public BlendShapeVector3s(int size)
		{
			deltaVetices = deltaNormals = deltaTangents = new Vector3[size];
		}
		public Vector3[] deltaVetices;
		public Vector3[] deltaNormals;
		public Vector3[] deltaTangents;
	}
	public static List<BlendShapeVector3s> GetBlendShapeVector3s (Mesh mesh) {
			List<BlendShapeVector3s> data = new List<BlendShapeVector3s>();
			
			var index = 0;
			for (int i = 0; i < mesh.blendShapeCount; i++)
			{
				for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(i); frame++)
				{
					var deltas = new BlendShapeVector3s(mesh.vertexCount);
					mesh.GetBlendShapeFrameVertices(
						i,
						frame, 
						deltas.deltaVetices, 
						deltas.deltaNormals, 
						deltas.deltaTangents);
					data.Add(deltas);
					index ++;
				}
			}
			return data;
	}
	public static Mesh blendshape_copy (Mesh target, List<BlendShapeVector3s> data , Mesh newMesh = null) {
		if (newMesh == null)
			newMesh = new Mesh();
		
		var index = 0;
		for (int i = 0; i < target.blendShapeCount; i++)
		{
			for (int frame = 0; frame < target.GetBlendShapeFrameCount(i); frame++)
			{
				newMesh.AddBlendShapeFrame(
					target.GetBlendShapeName(i),
					target.GetBlendShapeFrameWeight(i,frame),
					data[index].deltaVetices,
					data[index].deltaNormals,
					data[index].deltaTangents
				);
				index ++;
			}
		}

		return newMesh;
	}
	
	void CheckBlendshapes()
	{
		if (m_target == null) return;
		var blendShapeCount = m_target.blendShapeCount;
		
		if (blendShapeCount > 0)
		{
			for (int i = 0; i < blendShapeCount; i++) {
				m_blendShapes.Add(m_target.GetBlendShapeName(i));
			}
		}
	}
	
	void CopyGameObject(GameObject target)
	{
		if (!target.active) return;
		AddGameObject(GameObject.Instantiate(target));
	}
	
	public void SetBlendshape(object data)
	{
		int popupIndex = (int)data;
		if (popupIndex < 0 || popupIndex >= m_blendShapes.Count)
			return;

		activeBlendshape = popupIndex;

		DestroyBakedSkinnedMesh();
		BakeSkinnedMesh();
	}
	
	public void SetbakeBlendshapes(object data)
	{
		var popupdata = (BakeBlendshape)data;
		if (popupdata.m_blendshape < 0 || popupdata.m_blendshape >= m_blendShapes.Count)
			return;
	    	
		m_bakeBlendshapes.Add(popupdata);
	    
		DestroyBakedSkinnedMesh();
		BakeSkinnedMesh();
	}
	
	Mesh m_bakedSkinnedMesh;
	void DestroyBakedSkinnedMesh()
	{
		if (m_bakedSkinnedMesh)
			Object.DestroyImmediate(m_bakedSkinnedMesh);
	}
	
	void BakeSkinnedMesh()
	{
		if (m_target == null)
			return;
			
		var baseGameObjectForSkinnedMeshRenderer = new GameObject()
			{hideFlags = HideFlags.HideAndDontSave}
			;
		SkinnedMeshRenderer skinnedMeshRenderer = baseGameObjectForSkinnedMeshRenderer.AddComponent<SkinnedMeshRenderer>();
		
		m_bakedSkinnedMesh = new Mesh() 
			{hideFlags = HideFlags.HideAndDontSave}
			;
			
		var isRigid = m_target.blendShapeCount > 0 && m_target.bindposes.Length == 0;
		
		Transform[] boneTransforms = new Transform[m_target.bindposes.Length];
		
		if (!isRigid) //ブレンドシェイプがない　OR　ボーンがある時
		{
			for (int i = 0; i < boneTransforms.Length; i++) {
				var bindPoseInvese = m_target.bindposes[i].inverse;
				boneTransforms[i] = new GameObject().transform;
				boneTransforms[i].gameObject.hideFlags = HideFlags.HideAndDontSave;
				SetTransformMatrix(boneTransforms[i],bindPoseInvese);
			}
			skinnedMeshRenderer.bones = boneTransforms;
		}
		
		skinnedMeshRenderer.sharedMesh = m_target;
		
		if (m_bakeBlendshapes.Count > 0)
		{
			foreach (var bake in m_bakeBlendshapes)
			{
				if (bake.m_blendshape >= 0)
					skinnedMeshRenderer.SetBlendShapeWeight(bake.m_blendshape, bake.m_blendweight);
			}
		}
		
		if (activeBlendshape >= 0)
			skinnedMeshRenderer.SetBlendShapeWeight(activeBlendshape, BlendshapeWeight);
			
		skinnedMeshRenderer.BakeMesh(m_bakedSkinnedMesh);
		
		m_bakedSkinnedMesh.RecalculateBounds();
		
		skinnedMeshRenderer.sharedMesh = null;
		
		Object.DestroyImmediate(skinnedMeshRenderer);
		Object.DestroyImmediate(baseGameObjectForSkinnedMeshRenderer);
		
		if (!isRigid)
		{
			foreach (var bone in boneTransforms)
			{
				Object.DestroyImmediate(bone.gameObject);
			}
		}
	}
	
	void SetTransformMatrix(Transform tr, Matrix4x4 matrix)
	{
		var pos = new Vector3(matrix.m03, matrix.m13, matrix.m23);
		
		var scale = matrix.lossyScale;
		
		var invScale = new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f / scale.z); 
		matrix.m00 *= invScale.x; matrix.m10 *= invScale.x; matrix.m20 *= invScale.x;
		matrix.m01 *= invScale.y; matrix.m11 *= invScale.y; matrix.m21 *= invScale.y;
		matrix.m02 *= invScale.z; matrix.m12 *= invScale.z; matrix.m22 *= invScale.z;
		
		var rot = matrix.rotation;
		tr.localPosition = pos;
		tr.localRotation = rot;
		tr.localScale = scale;
	}
#endregion

#region Rendering
	internal void RenderMeshPreview(
		Mesh mesh,
		int meshSubset)
	{
		if (mesh == null) return;
		
		Bounds bounds = mesh.bounds;
		Transform renderCamTransform = Camera.transform;
		Camera.nearClipPlane = 0.0001f;
		Camera.farClipPlane = 1000f;
		
		float halfSize = bounds.extents.magnitude;
		float distance = 3.0f * halfSize;
		
		Camera.orthographic = false;
		Quaternion camRotation = Quaternion.identity;
		Vector3 camPosition =
			camRotation * Vector3.forward *
			( -distance * m_zoomFactor ) + m_pivotPositionOffset;
		
		renderCamTransform.position = camPosition;
		renderCamTransform.rotation = camRotation;
		
		RenderMeshPreviewSkipCameraAndLighting(mesh,bounds,meshSubset);
	}
	
	internal void RenderMeshPreviewSkipCameraAndLighting(
		Mesh mesh,
		Bounds bounds,
		int meshSubset) // -1 for whole mesh
	{
		if (mesh == null) return;
		
		Vector2 Dir = m_previewDir;
		Quaternion rot = Quaternion.Euler(Dir.x,0,0) * Quaternion.Euler(0,Dir.y,0) * m_meshRotationOffset;
		Vector3 pos = rot * (-bounds.center);
		
		bool oldFog = RenderSettings.fog;
		Unsupported.SetRenderSettingsUseFogNoDirty(false);
		
		int submeshes = mesh.subMeshCount;
		var tintSubmeshes = false;
		var colorPropID = 0;
		MaterialPropertyBlock customProperties = null;
		if (submeshes > 1 && meshSubset == -1)
		{
			tintSubmeshes = true;
			customProperties = new MaterialPropertyBlock();
			colorPropID = Shader.PropertyToID("_Color");
		}
		
		Camera.clearFlags = CameraClearFlags.SolidColor;
		Camera.backgroundColor = Color.gray;
		if (meshSubset < 0 || meshSubset >= submeshes)
		{
			for (int i = 0; i < submeshes; i++) {
				if (tintSubmeshes)
					customProperties.SetColor(colorPropID,GetSubMeshTint(i));
				Graphics.DrawMesh(m_bakedSkinnedMesh == null ? mesh : m_bakedSkinnedMesh,pos,rot,m_materials[i],0,Camera,i);
				//m_material.SetPass(0);
				//Graphics.DrawMeshNow(mesh,pos,rot);
			}
		}
		else
		Graphics.DrawMesh(mesh,pos,rot,m_material,0,Camera,meshSubset,customProperties);
			//Graphics.DrawMeshNow(mesh,pos,rot);
			
		Unsupported.SetRenderSettingsUseFogNoDirty(oldFog);
		Camera.Render();
	}
	
	internal static Color GetSubMeshTint(int index)
	{
		var hue = Mathf.Repeat(index * 0.618f, 1);
		var sat = index == 0 ? 0f : 0.3f;
		var val = 1f;
		return Color.HSVToRGB(hue, sat, val);
	}
	
	public void Render()
	{
		if (!m_didInitialize) return;
		
		if (!RenderTexture || !Camera.targetTexture || RenderTexture.width != RenderTextureSize.x || RenderTexture.height != RenderTextureSize.y)
		{
			if (RenderTexture)
			{
				Object.DestroyImmediate(RenderTexture);
				RenderTexture = null;
			}
			
			var format = Camera.allowHDR ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm;
		RenderTexture = new RenderTexture(RenderTextureSize.x,RenderTextureSize.y,32, format);
			Camera.targetTexture = RenderTexture;
		}
		var oldAllowPipes = Unsupported.useScriptableRenderPipeline;
		Unsupported.useScriptableRenderPipeline = false;
		Camera.Render();
		Unsupported.useScriptableRenderPipeline = oldAllowPipes;
		
		//Debug.Log("Render");
		
		RenderMeshPreview(m_target,-1);
	}
	
	public void BeginPreview(Rect rect, GUIStyle previewBackground)
	{
		InitPreview(rect);
		Camera.backgroundColor = GUI.backgroundColor;
		
		if (previewBackground == null || previewBackground == GUIStyle.none || previewBackground.normal.background == null)
			return;
		
		Graphics.DrawTexture(
			previewBackground.overflow.Add(new Rect(0, 0, RenderTexture.width, RenderTexture.height)),
			previewBackground.normal.background , new Rect(0, 0, 1, 1), previewBackground.border.left,
			previewBackground.border.right, previewBackground.border.top, previewBackground.border.bottom,
			new Color(.5f, .5f, .5f, 0.5f), null
		);
	}
	
	void InitPreview(Rect r)
	{
		float scalseFact = EditorGUIUtility.pixelsPerPoint;
		
		int rtWidth = (int)(r.width * scalseFact);
		int rtHight = (int)(r.height * scalseFact);
		
		RenderTextureSize = new Vector2Int(rtWidth,rtHight);
		
		//if(Event.current != null && Event.current.type == EventType.Repaint)
		//	Camera.pixelRect = new Rect(0,0,rtWidth,rtHight);
		//else if (Event.current != null && Event.current.type == EventType.Layout)
		//	Camera.pixelRect = EditorGUIUtility.PointsToPixels(r);
	}
	
	float GetScaleFactor(float width, float height)
	{
		float scaleFacX = Mathf.Max(Mathf.Min(width * 2, 1024), width)/width;
		float scaleFacY = Mathf.Max(Mathf.Min(height * 2, 1024), height)/height;
		float result = Mathf.Min(scaleFacX, scaleFacY) * EditorGUIUtility.pixelsPerPoint;
		if (false)
			result = Mathf.Max(Mathf.Round(result), 1f);
		return result;
	}
	
	public Texture EndPreview()
	{
		Unsupported.RestoreOverrideLightingSettings();

		//m_SavedState.Restore();
		FinishFrame();
		//m_previewOpened = false;
		return RenderTexture;
	}

	private void FinishFrame()
	{
		Unsupported.RestoreOverrideLightingSettings();
		//foreach (var light in lights)
		//m_light.enabled = false;
	}

	public void EndAndDrawPreview(Rect r)
	{
		var texture = EndPreview();
		DrawPreview(r, texture);
	}

	internal static void DrawPreview(Rect r, Texture texture)
	{
		GUI.DrawTexture(r, texture, ScaleMode.StretchToFill, false);
	}
	
	public void Render(Rect rect, GUIStyle background)
	{	
		BeginPreview(rect,background);
		Render();
		EndAndDrawPreview(rect);	
	}
#endregion

#region PreviewGUI
	public void OnInteractivePreviewGUI(Rect rect, GUIStyle background)
	{
		var evt = Event.current;
		
		if(!ShaderUtil.hardwareSupportsRectRenderTexture)
		{
			if (evt.type == EventType.Repaint)
				EditorGUI.DropShadowLabel(new Rect(rect.x, rect.y, rect.width, 40), "Mesh preview requiers\nrender textuer support");
			return;
		}
		
		if(evt.type == EventType.ValidateCommand || evt.type == EventType.ExecuteCommand)
		{
			
		}
		
		if(evt.button <= 0)
			m_lightDir = Drag2D(m_lightDir, rect, evt);
		if(evt.button == 1)
			m_previewDir = Drag2D(m_previewDir, rect, evt);
		if(evt.type == EventType.ScrollWheel)
			MeshPreviewZoom(rect, evt);
		if(evt.type == EventType.MouseDrag && evt.button == 2)
			MeshPreviewPan(rect, evt);
			
		if(evt.type != EventType.Repaint)
			return;
			
		Render(rect,background);
	}
	
	Vector2 Drag2D(Vector2 scrollPosition, Rect position, Event evt)
	{
		int id = GUIUtility.GetControlID(FocusType.Passive);
		switch (evt.GetTypeForControl(id))
		{
		case EventType.MouseDown:
			if(position.Contains(evt.mousePosition) && position.width > 50)
			{
				GUIUtility.hotControl = id;
				evt.Use();
				EditorGUIUtility.SetWantsMouseJumping(1);
			}
			break;
		case EventType.MouseDrag:
			if (GUIUtility.hotControl == id)
			{
				scrollPosition -= new Vector2(evt.delta.y,evt.delta.x) * (evt.shift ? 3 : 1) / Mathf.Min(position.width, position.height) * 140.0f;
				evt.Use();
				GUI.changed = true;
			}
			break;
		case EventType.MouseUp:
			if(GUIUtility.hotControl == id)
				GUIUtility.hotControl = 0;
			EditorGUIUtility.SetWantsMouseJumping(0);
			break;
		default:
			break;
		}
		return scrollPosition;
	}
	
	void MeshPreviewZoom(Rect rect, Event evt)
	{
		float zoomDelta = -(HandleUtility.niceMouseDeltaZoom * 0.5f) * 0.05f;
		var zoomFactor = m_zoomFactor;	
		var newZoom = zoomFactor + zoomFactor * zoomDelta;
		newZoom = Mathf.Clamp(newZoom, 0.1f, 10.0f);
		
		//Zoom around current mouse position
		var mouseViewPos = new Vector2(
			evt.mousePosition.x / rect.width,
			1 - evt.mousePosition.y / rect.height);
		var mouseWorldPos = Camera.ViewportToWorldPoint(mouseViewPos);
		var orthoPos = new Vector3(0.5f,0.5f,-1);
		var mouseToCamPos = orthoPos - mouseWorldPos;
		var newCamPos = mouseWorldPos + mouseToCamPos * (newZoom / zoomFactor);
		orthoPos = new Vector3(newCamPos.x, newCamPos.y, orthoPos.z);
		
		m_zoomFactor = newZoom;
		//Debug.Log(m_zoomFactor);
		evt.Use();
	}
	
	void MeshPreviewPan(Rect rect, Event evt)
	{
		var cam = Camera;
		
		var delta = new Vector3(
			-evt.delta.x * cam.pixelWidth / rect.width,
			evt.delta.y * cam.pixelHeight / rect.height,
			0);
			
		Vector3 screenPos;
		Vector3 worldPos;
		var pivotPositionOffset = m_pivotPositionOffset;
		screenPos = cam.WorldToScreenPoint(pivotPositionOffset);
		screenPos += delta;
		worldPos = cam.ScreenToWorldPoint(screenPos) - pivotPositionOffset;
		
		m_pivotPositionOffset += worldPos;
		//Debug.Log(m_pivotPositionOffset);	
		evt.Use();
	}
	
	public void OnPreviewSettings ()
	{
		if (!ShaderUtil.hardwareSupportsRectRenderTexture)
			return;
			
		GUI.enabled = true;
		
		float blendshapesDropDownWidth = EditorStyles.toolbarDropDown.CalcSize(new GUIContent("Blendshapes")).x;
		Rect blendshapesDropdownRect = EditorGUILayout.GetControlRect(GUILayout.Width(blendshapesDropDownWidth));
		blendshapesDropdownRect.y -= 1;
		blendshapesDropdownRect.x += 5;
		GUIContent blendshape = new GUIContent(m_blendShapes.Count > 0 ? m_blendShapes[activeBlendshape < 0 ? 0 : activeBlendshape] : "none", "Active blendshape name");
		
		if (EditorGUI.DropdownButton(blendshapesDropdownRect,blendshape,FocusType.Passive,EditorStyles.toolbarDropDown))
		{
			
		}
	}
	
	public virtual string GetInfoString()
	{
		return "Test";
	}
#endregion

#region Dispose
	public void	Dispose()
	{
		//Debug.Log("Dispose");
		Camera.targetTexture = null;
		if (RenderTexture != null)
		{
			Object.DestroyImmediate(RenderTexture);
			RenderTexture = null;
		}
		Object.DestroyImmediate(m_target);
		Object.DestroyImmediate(m_light);
		Object.DestroyImmediate(Camera);
		
		BlendShapes.Clear();
		BakeBlendshapes.Clear();
		
		m_target = null;
		m_material = null;
		
		EditorSceneManager.ClosePreviewScene(Scene);
	}
	
	public void ResetView()
	{
		m_zoomFactor = 1.0f;
		m_scaleFactor = 1.0f;
		m_pivotPositionOffset = Vector3.zero;
		activeBlendshape = 0;
		m_previewDir = Vector2.up * 180;
	}
#endregion
	
#region Othore	
	/// <summary>
	/// Add GameObject to preview scene
	/// </summary>
	public void AddGameObject(GameObject go)
	{
		SceneManager.MoveGameObjectToScene(go, Scene);
	}
#endregion
}
}