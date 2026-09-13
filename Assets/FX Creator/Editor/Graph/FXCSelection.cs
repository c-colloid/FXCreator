using System;
using System.Collections.Generic;

namespace colloid.FXCreator.Graph
{
	/// <summary>
	/// グラフの選択集合。ノードとエッジを別々に持つ。
	/// ビュー要素そのものではなくIDを持つので、ビューを作り直しても選択が生き残る
	/// （<see cref="FXCGraphView"/> は外部変更のたびに全再構築する設計）。
	/// </summary>
	public sealed class FXCSelection
	{
		private readonly HashSet<string> _nodes = new HashSet<string>();
		private readonly HashSet<string> _edges = new HashSet<string>();

		public event Action Changed;

		public IReadOnlyCollection<string> Nodes => _nodes;
		public IReadOnlyCollection<string> Edges => _edges;
		public int Count => _nodes.Count + _edges.Count;
		public bool IsEmpty => Count == 0;

		public bool ContainsNode(string nodeId) => nodeId != null && _nodes.Contains(nodeId);
		public bool ContainsEdge(string edgeId) => edgeId != null && _edges.Contains(edgeId);

		public void Clear()
		{
			if (Count == 0)
			{
				return;
			}
			_nodes.Clear();
			_edges.Clear();
			Changed?.Invoke();
		}

		public void SelectOnlyNode(string nodeId)
		{
			if (nodeId == null)
			{
				return;
			}
			if (_nodes.Count == 1 && _edges.Count == 0 && _nodes.Contains(nodeId))
			{
				return;
			}
			_nodes.Clear();
			_edges.Clear();
			_nodes.Add(nodeId);
			Changed?.Invoke();
		}

		public void SelectOnlyEdge(string edgeId)
		{
			if (edgeId == null)
			{
				return;
			}
			if (_edges.Count == 1 && _nodes.Count == 0 && _edges.Contains(edgeId))
			{
				return;
			}
			_nodes.Clear();
			_edges.Clear();
			_edges.Add(edgeId);
			Changed?.Invoke();
		}

		/// <summary>Ctrl/Shift クリック相当。既に入っていれば外す。</summary>
		public void ToggleNode(string nodeId)
		{
			if (nodeId == null)
			{
				return;
			}
			if (!_nodes.Remove(nodeId))
			{
				_nodes.Add(nodeId);
			}
			Changed?.Invoke();
		}

		public void ToggleEdge(string edgeId)
		{
			if (edgeId == null)
			{
				return;
			}
			if (!_edges.Remove(edgeId))
			{
				_edges.Add(edgeId);
			}
			Changed?.Invoke();
		}

		/// <summary>矩形選択の確定。<paramref name="additive"/> なら既存の選択に足す。</summary>
		public void SetNodes(IEnumerable<string> nodeIds, bool additive)
		{
			if (!additive)
			{
				_nodes.Clear();
				_edges.Clear();
			}
			foreach (string id in nodeIds)
			{
				if (id != null)
				{
					_nodes.Add(id);
				}
			}
			Changed?.Invoke();
		}

		/// <summary>
		/// ビュー再構築後に、もう存在しないIDを落とす。
		/// 消えた要素を選択したままにしないため。
		/// </summary>
		public void Prune(ICollection<string> liveNodeIds, ICollection<string> liveEdgeIds)
		{
			int removed = _nodes.RemoveWhere(id => !liveNodeIds.Contains(id));
			removed += _edges.RemoveWhere(id => !liveEdgeIds.Contains(id));
			if (removed > 0)
			{
				Changed?.Invoke();
			}
		}
	}
}
