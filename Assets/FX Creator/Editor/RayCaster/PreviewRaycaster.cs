using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class PreviewRaycaster : BaseRaycaster
{
	PreviewControl _root;
	Camera _camera;
	float _screenPlaneDistance;
	
	public override Camera eventCamera
	{
		get;
	}
	
	public override void Raycast(
		PointerEventData eventData,
		List<RaycastResult> resultAppendList)
	{
		// 何かに当たるならイベントを取り、何にも当たらないならスルーする
		var sp = eventData.position;
		float x = sp.x;
		float y = sp.y;
		bool hit = false;
		bool isDragging = false;

		// ドラッグ中ならtrueにする。でないと諸々のイベントが取れなくなる
		if (isDragging)
		{
			hit = true;
		}
		// 何かに当たればtrue
		else if (_root.RaycastRecursive(0, 0, x, y) != null)
		{
			hit = true;
		}
		else
		{
			// 外れたら離したものとみなす。
			hit = false;
		}
		// 当たったらraycastResult足す
		if (hit)
		{
			var result = new RaycastResult
			{
				gameObject = gameObject, // 自分
				module = this,
				distance = _screenPlaneDistance,
				worldPosition = _camera.transform.position + (_camera.transform.forward * _screenPlaneDistance),
				worldNormal = -_camera.transform.forward,
				screenPosition = eventData.position,
				index = resultAppendList.Count,
				sortingLayer = 0,
				sortingOrder = 32767
			};
			resultAppendList.Add(result);
		}
	}
}
