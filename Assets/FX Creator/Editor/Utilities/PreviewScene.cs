using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using System.Linq;
using System.IO;
using VRC.SDK3.Avatars.Components;
using UnityEngine.Animations;

public class PreviewScene : IDisposable
{
	public Scene Scene { get; private set; }
	public Camera Camera { get; private set; } = null;
	public RenderTexture RenderTexture { get; private set; }
	public Vector2Int RenderTextureSize { get; set; } = new Vector2Int(1024, 1024);
	public GameObject Avatar { get; set; }

	private SavedRenderSettings m_savedRenderSettings;
	private List<GameObject> m_gameObjects = new List<GameObject>();
	private bool m_didInitialize = false;

	public PreviewScene(string environmentSceneAssetPath = null)
	{
		try {
			Scene = EditorSceneManager.NewPreviewScene();
        
		if (environmentSceneAssetPath != null) {
				m_savedRenderSettings = SavedRenderSettings.Create(environmentSceneAssetPath);
				CopyRootGameObjects(environmentSceneAssetPath);
			}

			// Deactivate unused cameras
			var oldCameras = Scene
				.GetRootGameObjects()
				.SelectMany(x => x.GetComponentsInChildren<Camera>());
			foreach (var oldCamera in oldCameras) {
				oldCamera.enabled = false;
			}

			var cameraGO = new GameObject("Preview Scene Camera", typeof(Camera));
			var avatar = Scene.GetRootGameObjects().Where(x => x.TryGetComponent(out VRCAvatarDescriptor avatar)).First().GetComponent<VRCAvatarDescriptor>();
			Avatar = avatar.gameObject;
			cameraGO.transform.position = avatar.ViewPosition + Vector3.forward;
			cameraGO.transform.LookAt(avatar.ViewPosition,Vector3.up);
			ConstraintSource source = new ConstraintSource();
			source.sourceTransform = avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
			source.weight = 1;
			AddGameObject(cameraGO);
			//cameraGO.AddComponent<ParentConstraint>();
			//cameraGO.GetComponent<ParentConstraint>().AddSource(source);
			cameraGO.transform.SetParent(avatar.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head));
			Camera = cameraGO.GetComponent<Camera>();
			Camera.cameraType = CameraType.Preview;
			Camera.forceIntoRenderTexture = true;
			Camera.scene = Scene;
			Camera.enabled = false; // Deactivate so as not to affect GameView
        
			var hasDirectionalLight = Scene
				.GetRootGameObjects()
				.SelectMany(x => x.GetComponentsInChildren<Light>())
				.Any(x => x.type == LightType.Directional);
			if (!hasDirectionalLight) {
				var lightGO = new GameObject("Directional Light", typeof(Light));
				AddGameObject(lightGO);
				lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
				var light = lightGO.GetComponent<Light>();
				light.type = LightType.Directional;
			}
            
			Debug.Log("Constract");
			m_didInitialize = true;
		}
			catch (Exception e) {
				Debug.Log("Catch");
				Dispose();
				m_didInitialize = false;
				throw e;
			}
	}

	public void Render(bool useScriptableRenderPipeline = false)
	{
		if (!m_didInitialize) {
			return;
		}
		// Change RenderSettings
		if (m_savedRenderSettings != null && Unsupported.SetOverrideLightingSettings(Scene)) {
			m_savedRenderSettings.Apply();
		}
        
		// Create RenderTexture if needed
		if (!RenderTexture || RenderTexture.width != RenderTextureSize.x || RenderTexture.height != RenderTextureSize.y)
		{
			if (RenderTexture)
			{
				Object.DestroyImmediate(RenderTexture);
				RenderTexture = null;
			}

			var format = Camera.allowHDR ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm;
			RenderTexture = new RenderTexture(RenderTextureSize.x, RenderTextureSize.y, 32, format);
		}
		Camera.targetTexture = RenderTexture;
        
		// Render
		var oldAllowPipes = Unsupported.useScriptableRenderPipeline;
		Unsupported.useScriptableRenderPipeline = useScriptableRenderPipeline;
		Camera.Render();
		Unsupported.useScriptableRenderPipeline = oldAllowPipes;

		Camera.targetTexture = null;
		// Restore RenderSettings
		if (m_savedRenderSettings != null) {
			Unsupported.RestoreOverrideLightingSettings();
		}
	}

	public void Dispose()
	{
		Debug.Log(Camera);
		Camera.targetTexture = null;

		if (RenderTexture != null){
			Object.DestroyImmediate(RenderTexture);
			RenderTexture = null;
		}

		foreach (var go in m_gameObjects){
			Object.DestroyImmediate(go);
		}
		m_gameObjects.Clear();

		EditorSceneManager.ClosePreviewScene(Scene);
	}
    
	/// <summary>
	/// Add GameObject to preview scene
	/// </summary>
	public void AddGameObject(GameObject go)
	{
		if (m_gameObjects.Contains(go)){
			return;
		}
		SceneManager.MoveGameObjectToScene(go, Scene);
		m_gameObjects.Add(go);
	}

	/// <summary>
	/// Add prefab instance to preview scene
	/// </summary>
	public GameObject InstantiatePrefab(GameObject prefab)
	{
		var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
		m_gameObjects.Add(instance);
		return instance;
	}

	/// <summary>
	/// Copy all root GameObjects of source scene to preview scene
	/// </summary>
	private void CopyRootGameObjects(string sourceSceneAssetPath)
	{
		GameObject[] rootGameObjects = null;
		for (int i = 0; i < EditorSceneManager.sceneCount; i++) {
			var scene = EditorSceneManager.GetSceneAt(i);
			if (scene.path == sourceSceneAssetPath) {
				rootGameObjects = scene.GetRootGameObjects();
				break;
			}
		}
		if (rootGameObjects == null) {
			var scene = EditorSceneManager.OpenScene(sourceSceneAssetPath, OpenSceneMode.Additive);
			rootGameObjects = scene.GetRootGameObjects();
			EditorSceneManager.CloseScene(scene, true);
		}
		if (rootGameObjects != null) {
			foreach (var rootGameObject in rootGameObjects) {
				if (!rootGameObject.active) continue;
				AddGameObject(GameObject.Instantiate(rootGameObject));
			}
		}
	}

	public class SavedRenderSettings
	{
		private Material m_skybox;
		private Light m_sun;
		private AmbientMode m_ambientMode;
		private SphericalHarmonicsL2 m_ambientProbe;
		private Color m_ambientSkyColor;
		private Color m_ambientEquatorColor;
		private Color m_ambientGroundColor;
		private Color m_ambientLight;
		private float m_ambientIntensity;
		private DefaultReflectionMode m_defaultReflectionMode;
		private int m_defaultReflectionResolution;
		private Cubemap m_customReflection;
		private float m_reflectionIntensity;
		private int m_reflectionBounces;
		private Color m_substractiveShadowColor;
		private bool m_fog;
		private FogMode m_fogMode;
		private Color m_fogColor;
		private float m_fogDensity;
		private float m_fogStartDistance;
		private float m_fogEndDistance;

		public static SavedRenderSettings Create(string scenePath)
		{
			var result = new SavedRenderSettings();
			Scene? oldActiveScene = null;
			if (scenePath != EditorSceneManager.GetActiveScene().path) {
				oldActiveScene = EditorSceneManager.GetActiveScene();
				var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
				EditorSceneManager.SetActiveScene(scene);
			}
            
			result.m_skybox = RenderSettings.skybox;
			result.m_sun = RenderSettings.sun;
			result.m_ambientMode = RenderSettings.ambientMode;
			result.m_ambientProbe = RenderSettings.ambientProbe;
			result.m_ambientSkyColor = RenderSettings.ambientSkyColor;
			result.m_ambientEquatorColor = RenderSettings.ambientEquatorColor;
			result.m_ambientGroundColor = RenderSettings.ambientGroundColor;
			result.m_ambientLight = RenderSettings.ambientLight;
			result.m_ambientIntensity = RenderSettings.ambientIntensity;
			result.m_defaultReflectionMode = RenderSettings.defaultReflectionMode;
			result.m_defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
			//result.m_customReflection = RenderSettings.customReflection;
			// If defaultReflectionMode is Skybox, search and set the created cube map
			if (result.m_defaultReflectionMode == DefaultReflectionMode.Skybox && Lightmapping.lightingDataAsset != null) {
				var lightingDataAssetPath = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
				var lightingDataAssetDirectoryName = Path.GetDirectoryName(lightingDataAssetPath);
				var environmentProbeAssetPath = Directory
					.GetFiles(lightingDataAssetDirectoryName)
					.FirstOrDefault(x => x.EndsWith(".exr"));
				if (!string.IsNullOrEmpty(environmentProbeAssetPath)) {
					result.m_defaultReflectionMode = DefaultReflectionMode.Custom;
					result.m_customReflection = AssetDatabase.LoadAssetAtPath<Cubemap>(environmentProbeAssetPath.Replace("\\", "/"));
				}
			}
			result.m_reflectionIntensity = RenderSettings.reflectionIntensity;
			result.m_reflectionBounces = RenderSettings.reflectionBounces;
			result.m_substractiveShadowColor = RenderSettings.subtractiveShadowColor;
			result.m_fog = RenderSettings.fog;
			result.m_fogMode = RenderSettings.fogMode;
			result.m_fogColor = RenderSettings.fogColor;
			result.m_fogDensity = RenderSettings.fogDensity;
			result.m_fogStartDistance = RenderSettings.fogStartDistance;
			result.m_fogEndDistance = RenderSettings.fogEndDistance;

			if (oldActiveScene.HasValue) {
				var scene = EditorSceneManager.GetActiveScene();
				EditorSceneManager.SetActiveScene(oldActiveScene.Value);
				EditorSceneManager.CloseScene(scene, true);
			}
			return result;
		}

		public void Apply()
		{
			RenderSettings.skybox = m_skybox;
			RenderSettings.sun = m_sun;
			RenderSettings.ambientMode = m_ambientMode;
			RenderSettings.ambientProbe = m_ambientProbe;
			RenderSettings.ambientSkyColor = m_ambientSkyColor;
			RenderSettings.ambientEquatorColor = m_ambientEquatorColor;
			RenderSettings.ambientGroundColor = m_ambientGroundColor;
			RenderSettings.ambientLight = m_ambientLight;
			RenderSettings.ambientIntensity = m_ambientIntensity;
			RenderSettings.defaultReflectionMode = m_defaultReflectionMode;
			RenderSettings.defaultReflectionResolution = m_defaultReflectionResolution;
			RenderSettings.customReflection = m_customReflection;
			RenderSettings.reflectionIntensity = m_reflectionIntensity;
			RenderSettings.reflectionBounces = m_reflectionBounces;
			RenderSettings.subtractiveShadowColor = m_substractiveShadowColor;
			RenderSettings.fog = m_fog;
			RenderSettings.fogMode = m_fogMode;
			RenderSettings.fogColor = m_fogColor;
			RenderSettings.fogDensity = m_fogDensity;
			RenderSettings.fogStartDistance = m_fogStartDistance;
			RenderSettings.fogEndDistance = m_fogEndDistance;
		}
	}
}