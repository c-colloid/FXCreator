using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public class SelectObjectOutline : IDisposable
{
	public Material emissionMaterial;
	public Material outlineMaterial;

	private new Camera m_camera;
	private CommandBuffer m_commandBuffer;
	private Renderer m_targetRenderer = null;
	
	public SelectObjectOutline(Camera camera){
		m_commandBuffer = new CommandBuffer();
		m_commandBuffer.name = "Selective Outline";
		
		emissionMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("ce5257b1d3b31b447ba7b81aef4971c2"));
		outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("ca6c55819c35cb94eafd3b7a32a0434a"));
		
		// ImageEffects前(OnRenderImageが呼ばれる前)に適用
		camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, m_commandBuffer);
		
		//m_targetRenderer = terget;
	}
	
	public void Dispose(){
		m_camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, m_commandBuffer);
	}
	
	public void SetCommandBuffer(Renderer target)
	{
		m_commandBuffer.Clear();
		m_targetRenderer = target;

		if (m_targetRenderer == null) return;
		
		// レンダリング結果を格納するテクスチャ作成
		var id = Shader.PropertyToID("_OutlineTex");
		m_commandBuffer.GetTemporaryRT(id, -1, -1, 24, FilterMode.Bilinear);
		m_commandBuffer.SetRenderTarget(id);

		// アウトラインを表示させたいメッシュの描画
		m_commandBuffer.ClearRenderTarget(false, true, Color.clear);
		m_commandBuffer.DrawRenderer(m_targetRenderer, emissionMaterial);

		// アウトラインを抽出して合成
		m_commandBuffer.Blit(id, BuiltinRenderTextureType.CameraTarget, outlineMaterial);
	}
}
