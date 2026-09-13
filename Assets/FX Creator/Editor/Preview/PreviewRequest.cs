using System;
using UnityEngine;

namespace colloid.FXCreator.Preview
{
	/// <summary>
	/// ノード1つぶんのプレビュー要求（Docs/FXCreator-Design.md §5.2）。
	///
	/// <see cref="Owner"/> は要求元のノードビュー。ノードが画面外へ出たときに
	/// <c>CancelAll(owner)</c> でまとめて取り下げるための目印で、値としては使わない。
	/// </summary>
	public struct PreviewRequest
	{
		public object Owner;
		public AnimationClip Clip;
		public float Time;
		public Vector2Int Size;
		public HumanBodyBones Focus;
		public float Fov;

		/// <summary>描き上がったテクスチャを受け取る。要求元が生きていないなら呼ばれない。</summary>
		public Action<Texture> OnRendered;

		/// <summary>
		/// キャッシュの同一性。<b>要求元は含めない</b>ので、同じクリップの同じ時刻を
		/// 複数のノードが要求しても1回しか描かない（実際、同じクリップを指す
		/// ステートは珍しくない）。
		/// </summary>
		public PreviewKey Key
		{
			get
			{
				return new PreviewKey
				{
					ClipId = Clip != null ? Clip.GetInstanceID() : 0,
					Time = Time,
					Width = Size.x,
					Height = Size.y,
					Focus = Focus,
					Fov = Fov
				};
			}
		}
	}

	/// <summary>キャッシュのキー。構造体なので辞書に入れても割り当てが起きない。</summary>
	public struct PreviewKey : IEquatable<PreviewKey>
	{
		public int ClipId;
		public float Time;
		public int Width;
		public int Height;
		public HumanBodyBones Focus;
		public float Fov;

		public bool Equals(PreviewKey other)
		{
			return ClipId == other.ClipId
				&& Time.Equals(other.Time)
				&& Width == other.Width
				&& Height == other.Height
				&& Focus == other.Focus
				&& Fov.Equals(other.Fov);
		}

		public override bool Equals(object obj) => obj is PreviewKey other && Equals(other);

		public override int GetHashCode()
		{
			int hash = ClipId;
			hash = (hash * 397) ^ Time.GetHashCode();
			hash = (hash * 397) ^ Width;
			hash = (hash * 397) ^ Height;
			hash = (hash * 397) ^ (int)Focus;
			hash = (hash * 397) ^ Fov.GetHashCode();
			return hash;
		}
	}
}
