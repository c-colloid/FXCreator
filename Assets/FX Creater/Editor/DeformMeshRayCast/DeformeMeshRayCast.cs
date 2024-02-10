using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeformeMeshRayCast : IDisposable
{
	SkinnedMeshRenderer skinner;
	GameObject DeformingObject;
	Vector3[] origverts;
	Vector3[] verts;
	Vector3[] vertexData;
	Vector3[] tverts;
	Vector3[] norms;
	Color[] cols;
	int[] tris;
	bool didMod = false;
	static Mesh mesher;
	Matrix4x4[] finalMatrix;
	float softRange = .05f;
	int i;
	Mesh dupMesh ;
	BoneWeight[] weights;
	Color ColorRand;
	bool doDebug = true;
	GameObject posTester;

	private float u;
	private float v;
	private float t; // distance from ray origin
	
	public DeformeMeshRayCast(GameObject deformingObject){
		DeformingObject = deformingObject;
		skinner = DeformingObject.GetComponent<SkinnedMeshRenderer>();
		mesher = skinner.sharedMesh;
		//mesher = mfilt.mesh;
		origverts = mesher.vertices;
		norms = mesher.normals;
		cols = mesher.colors;

		finalMatrix = new Matrix4x4[origverts.Length];
		vertexData =  mesher.vertices;
	
		// init a duplicate mesh
		dupMesh = new Mesh();
		dupMesh.vertices = mesher.vertices;
		dupMesh.triangles = mesher.triangles;	
		dupMesh.normals = mesher.normals;  
		//fps.inputstring = "\nVerts:" + origverts.Length.ToString() + "\nTris:" +   	mesher.triangles.Length.ToString();		
		weights=mesher.boneWeights; 
	}
	
	public void Dispose()
	{
		
	}
	
	public bool RayTriangleIntersect(Ray r, Vector3 vert0, Vector3 vert1, Vector3 vert2)
	{
	 	// this originally comes from http://www.graphics.cornell.edu/pubs/1997/MT97.pdf
		 t = 0; 
		 v = 0;
		 u = 0;
		 var edge1 = vert1 - vert0;
		 var edge2 = vert2 - vert0;
		 var pvec = Vector3.Cross(r.direction, edge2);
		 var det = Vector3.Dot(edge1, pvec);
		 if (det > -0.00001)
		 {
			 return false;    	
		 }
		 float inv_det = 1.0f / det;
		 var tvec = r.origin - vert0;
		 u = Vector3.Dot(tvec, pvec) * inv_det;
		 if (u < -0.001 || u > 1.001)
			 return false;
		 var qvec = Vector3.Cross(tvec, edge1);
		 v = Vector3.Dot(r.direction, qvec) * inv_det;
		 if (v < -0.001 || u + v > 1.001)
			 return false;
		 t = Vector3.Dot(edge2, qvec) * inv_det;
		 if (t <= 0)
			 return false;  
		 return true;
	}

	public void getDeformedMesh(){
		Matrix4x4[] matrices = new Matrix4x4[skinner.bones.Length]; 
		for (i = 0; i < matrices.Length; i++) 
			matrices[i] = skinner.bones[i].localToWorldMatrix * mesher.bindposes[i];
         	
	
		for(i=0;i<origverts.Length;i++){ 
			BoneWeight weight = weights[i] ; 
			Matrix4x4 m0 = matrices[weight.boneIndex0]; 
			Matrix4x4 m1 = matrices[weight.boneIndex1]; 
			Matrix4x4 m2 = matrices[weight.boneIndex2]; 
			Matrix4x4 m3 = matrices[weight.boneIndex3]; 
			finalMatrix[i] = Matrix4x4.identity; 
			for(var n=0;n<16;n++){ 
				m0[n] *= weight.weight0; 
				m1[n] *= weight.weight1; 
				m2[n] *= weight.weight2; 
				m3[n] *= weight.weight3; 
				finalMatrix[i][n] = m0[n]+m1[n]+m2[n]+m3[n]; 
			} 
			vertexData[i] = finalMatrix[i].MultiplyPoint3x4(origverts[i]); 
			//vertexData[i].normal = finalMatrix[i].MultiplyVector(normals[i]); 
		}	
		dupMesh.vertices = vertexData;
		dupMesh.RecalculateNormals();
	}

	public bool GetRayCast(Ray ray, out GameObject hit){
		//var mousePos = Input.mousePosition;
		//var ray = Camera.main.ScreenPointToRay (mousePos);
	

		hit = null;
		getDeformedMesh();
		tris =  dupMesh.triangles;
		norms = dupMesh.normals;
		verts = dupMesh.vertices;
		if (doDebug)
			Debug.DrawLine( ray.origin, ray.origin + ray.direction*100, Color.red);
	  	
		for (i = 0; i < tris.Length; i+=3){
			
			if (Vector3.Dot( norms[tris[i + 0]], ray.direction) > 0)
				continue;
			if (doDebug){
				Debug.DrawLine( verts[tris[i + 0]], verts[tris[i + 1]]);
				Debug.DrawLine( verts[tris[i + 1]], verts[tris[i + 2]]);
				Debug.DrawLine( verts[tris[i + 2]], verts[tris[i + 0]]);
			}
			if (RayTriangleIntersect(ray, verts[tris[i + 0]], verts[tris[i + 2]], verts[tris[i + 1]]))
			{
				//var storepos1 = mousePos;
				//hitfaceCenter = (verts[tris[i + 0]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3.0 ;
				//var hitLocation =  (1 - u - v)  * verts[tris[i + 0]] + v * verts[tris[i + 1]] + u * verts[tris[i + 2]];
				//var faceNorm = (1 - u - v)  * norms[tris[i + 0]] + v * norms[tris[i + 1]] + u * norms[tris[i + 2]];
				hit = DeformingObject;
				return true;
			}
		}
		return false;
	}

	void Update () {
		var dt = Time.deltaTime;
		if (Input.GetMouseButton (0)) {
			// Construct a ray from the current mouse coordinates\
			var mousePos = Input.mousePosition;
			var ray = Camera.main.ScreenPointToRay (mousePos);
	
	
			ray = Camera.main.ScreenPointToRay(mousePos);
			getDeformedMesh();
			tris =  dupMesh.triangles;
			norms = dupMesh.normals;
			verts = dupMesh.vertices;
			if (doDebug)
				Debug.DrawLine( ray.origin, ray.origin + ray.direction*100, Color.red);
	  	
			for (i = 0; i < tris.Length; i+=3){
			
				if (Vector3.Dot( norms[tris[i + 0]], ray.direction) > 0)
					continue;
				if (doDebug){
					Debug.DrawLine( verts[tris[i + 0]], verts[tris[i + 1]]);
					Debug.DrawLine( verts[tris[i + 1]], verts[tris[i + 2]]);
					Debug.DrawLine( verts[tris[i + 2]], verts[tris[i + 0]]);
				}
				if (RayTriangleIntersect(ray, verts[tris[i + 0]], verts[tris[i + 2]], verts[tris[i + 1]]))
				{
					var storepos1 = mousePos;
					//hitfaceCenter = (verts[tris[i + 0]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3.0 ;
					var hitLocation =  (1 - u - v)  * verts[tris[i + 0]] + v * verts[tris[i + 1]] + u * verts[tris[i + 2]];
					var faceNorm = (1 - u - v)  * norms[tris[i + 0]] + v * norms[tris[i + 1]] + u * norms[tris[i + 2]];
					GameObject go = GameObject.Instantiate(posTester, hitLocation,Quaternion.LookRotation(faceNorm));
					GameObject.Destroy (go, .5f);
					didMod = true;
				}
			}
		}	    				
	
	}
	void LateUpdate(){
		if (didMod){
			mesher.colors = cols;
			didMod = false;
		}
	}
}
