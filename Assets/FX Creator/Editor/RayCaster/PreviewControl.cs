using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace colloid.FXCreator.Preview
{

public class PreviewControl : IDisposable
{
	public string name{ get; private set; }
	public bool enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			var oldValue = _enabled;
			_enabled = value;
			if (!oldValue && value) //有効化イベントを下流に流す
			{
				OnEnableRecursive();
			}
		}
	}
	protected bool eventEnabled{ get; set; }
	
	public float localLeftX{ get; private set; }
	public float localTopY{ get; private set; }
	public float width{ get; private set; }
	public float height{ get; private set; }
	
	private PreviewControl _nextBrother;
	private PreviewControl _firstChild;
	
	private bool _enabled;
	
	public PreviewControl(string name = "")
	{
		this.name = name;
		_enabled = true;
		eventEnabled = false;
	}
	
	public virtual void Dispose()
	{
	}
	
	protected virtual void OnEnable()
	{
	}
	
	public void OnEnableRecursive()
	{
		Debug.Assert(_enabled);
		OnEnable(); // 自分を呼ぶ

		var child = _firstChild;
		while (child != null)
		{
			if (child.enabled)
			{
				child.OnEnableRecursive();
			}
			child = child._nextBrother;
		}
	}
	
	public bool RaycastRecursive(
		float offsetX,
		float offsetY,
		float pointerX,
		float pointerY)
	{
		// 無効ならfalse
		if (!_enabled)
		{
			return false;
		}
		// グローバル座標を計算して子に回す
		float globalLeftX = offsetX + localLeftX;
		float globalTopY = offsetY + localTopY;

		var child = _firstChild;
		while (child != null)
		{
			bool result = child.RaycastRecursive(
				globalLeftX,
				globalTopY,
				pointerX,
				pointerY);
			if (result)
			{
				return true;
			}
			child = child._nextBrother;
		}

		// イベント取る場合のみ判定
		if (eventEnabled)
		{
			// 自分の当たり判定
			float globalRightX = globalLeftX + width;
			float globalBottomY = globalTopY + height;
			if (
				(pointerX >= globalLeftX)
				&& (pointerX < globalRightX)
				&& (pointerY >= globalTopY)
				&& (pointerY < globalBottomY))
			{
				return true;
			}
		}
		return false;
	}
}
}
