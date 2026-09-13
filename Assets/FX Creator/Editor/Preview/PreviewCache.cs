using System;
using System.Collections.Generic;
using UnityEngine;

namespace colloid.FXCreator.Preview
{
	/// <summary>
	/// 描き上がったプレビューの LRU キャッシュ（Docs/FXCreator-Design.md §5.2）。
	///
	/// 保持しているのは <see cref="RenderTexture"/> なので、<b>追い出すときに
	/// 必ず解放する</b>のがこのクラスの存在理由。ここを怠ると、グラフを
	/// スクロールするだけで GPU メモリが増え続ける（Phase 5 のゲートそのもの）。
	/// </summary>
	public sealed class PreviewCache : IDisposable
	{
		private sealed class Entry
		{
			public PreviewKey Key;
			public RenderTexture Texture;
		}

		private readonly Dictionary<PreviewKey, LinkedListNode<Entry>> _byKey =
			new Dictionary<PreviewKey, LinkedListNode<Entry>>();

		/// <summary>先頭が最近使ったもの。末尾から追い出す。</summary>
		private readonly LinkedList<Entry> _lru = new LinkedList<Entry>();

		private readonly int _capacity;

		public PreviewCache(int capacity = 64)
		{
			_capacity = Mathf.Max(1, capacity);
		}

		public int Count => _byKey.Count;

		public int Capacity => _capacity;

		/// <summary>いま抱えているテクスチャのおおよそのバイト数（ゲートの確認用）。</summary>
		public long ApproximateBytes
		{
			get
			{
				long total = 0;
				foreach (Entry entry in _lru)
				{
					if (entry.Texture != null)
					{
						// 32bit カラー + 24bit デプス ≒ 1画素 8 バイト。
						total += (long)entry.Texture.width * entry.Texture.height * 8L;
					}
				}
				return total;
			}
		}

		public bool TryGet(PreviewKey key, out RenderTexture texture)
		{
			texture = null;
			LinkedListNode<Entry> node;
			if (!_byKey.TryGetValue(key, out node))
			{
				return false;
			}

			// テクスチャは Unity 側の都合（解像度変更・ドメインリロード）で
			// 死ぬことがある。死んでいたら未ヒット扱いにして作り直させる。
			if (node.Value.Texture == null)
			{
				_lru.Remove(node);
				_byKey.Remove(key);
				return false;
			}

			_lru.Remove(node);
			_lru.AddFirst(node);
			texture = node.Value.Texture;
			return true;
		}

		public void Add(PreviewKey key, RenderTexture texture)
		{
			if (texture == null)
			{
				return;
			}

			LinkedListNode<Entry> existing;
			if (_byKey.TryGetValue(key, out existing))
			{
				// 同じキーで描き直した。古い方を確実に解放してから差し替える。
				if (existing.Value.Texture != null && existing.Value.Texture != texture)
				{
					Release(existing.Value.Texture);
				}
				existing.Value.Texture = texture;
				_lru.Remove(existing);
				_lru.AddFirst(existing);
				return;
			}

			var node = new LinkedListNode<Entry>(new Entry { Key = key, Texture = texture });
			_lru.AddFirst(node);
			_byKey.Add(key, node);

			while (_byKey.Count > _capacity)
			{
				LinkedListNode<Entry> last = _lru.Last;
				_lru.RemoveLast();
				_byKey.Remove(last.Value.Key);
				Release(last.Value.Texture);
			}
		}

		public void Clear()
		{
			foreach (Entry entry in _lru)
			{
				Release(entry.Texture);
			}
			_lru.Clear();
			_byKey.Clear();
		}

		public void Dispose()
		{
			Clear();
		}

		private static void Release(RenderTexture texture)
		{
			if (texture == null)
			{
				return;
			}
			texture.Release();
			UnityEngine.Object.DestroyImmediate(texture);
		}
	}
}
