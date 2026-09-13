using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace colloid.FXCreator.Graph
{
	public enum FXCPortDirection
	{
		Input,
		Output
	}

	/// <summary>ノードとポートの組でポートを一意に指す。</summary>
	public readonly struct FXCPortRef : IEquatable<FXCPortRef>
	{
		public readonly string NodeId;
		public readonly string PortId;

		public FXCPortRef(string nodeId, string portId)
		{
			NodeId = nodeId;
			PortId = portId;
		}

		public bool IsValid => !string.IsNullOrEmpty(NodeId);

		public bool Equals(FXCPortRef other)
		{
			return string.Equals(NodeId, other.NodeId, StringComparison.Ordinal)
				&& string.Equals(PortId, other.PortId, StringComparison.Ordinal);
		}

		public override bool Equals(object obj) => obj is FXCPortRef other && Equals(other);

		public override int GetHashCode()
		{
			int h = NodeId != null ? NodeId.GetHashCode() : 0;
			return (h * 397) ^ (PortId != null ? PortId.GetHashCode() : 0);
		}

		public override string ToString() => NodeId + "/" + PortId;
	}

	/// <summary>右クリックされた場所の情報。</summary>
	public struct FXCGraphContext
	{
		/// <summary>クリック位置（グラフ座標）。</summary>
		public Vector2 GraphPosition;

		/// <summary>ノード上ならそのID、でなければ null。</summary>
		public string NodeId;

		/// <summary>エッジ上ならそのID、でなければ null。</summary>
		public string EdgeId;
	}

	public interface IFXCGraphPort
	{
		string Id { get; }

		/// <summary>ポート名。ツールチップに出す。空なら出さない。</summary>
		string Name { get; }

		FXCPortDirection Direction { get; }

		Color Color { get; }
	}

	public interface IFXCGraphNode
	{
		/// <summary>ビュー再構築をまたいでノードビューを対応付けるための安定ID。</summary>
		string Id { get; }

		string Title { get; }

		/// <summary>2行目（Motion 名など）。null なら表示しない。</summary>
		string Subtitle { get; }

		/// <summary>
		/// グラフ空間の位置とサイズ。AnimatorController の
		/// <c>ChildAnimatorState.position</c> と同じ座標系。
		/// </summary>
		Rect GraphRect { get; }

		/// <summary>左端のアクセント帯の色（状態の種類を示す）。</summary>
		Color AccentColor { get; }

		/// <summary>ポート。空でもよい（その場合エッジはノードの右端→左端に繋がる）。</summary>
		IReadOnlyList<IFXCGraphPort> Ports { get; }
	}

	public interface IFXCGraphEdge
	{
		string Id { get; }

		string FromNodeId { get; }

		/// <summary>出力ポートID。null ならノードの右端中央から出る。</summary>
		string FromPortId { get; }

		string ToNodeId { get; }

		/// <summary>入力ポートID。null ならノードの左端中央に入る。</summary>
		string ToPortId { get; }

		Color Color { get; }
	}

	/// <summary>
	/// ビュー（<see cref="FXCGraphView"/> 以下）とドメインの境界
	/// （Docs/FXCreator-Design.md §3.4）。
	///
	/// <c>Graph/</c> は Animator の存在を知らない。Phase 2 で
	/// <c>AcGraphSource</c> がこの境界を AnimatorController 向けに実装し、
	/// 将来 ③ExMenu や ④Contact のグラフでも同じビューを使い回す。
	/// </summary>
	public interface IFXCGraphSource
	{
		IReadOnlyList<IFXCGraphNode> Nodes { get; }
		IReadOnlyList<IFXCGraphEdge> Edges { get; }

		/// <summary>ドメイン側が変わった（＝ビューを作り直してほしい）通知。</summary>
		event Action Changed;

		bool CanMoveNodes { get; }

		/// <summary>false なら接続・削除・コンテキストメニューを出さない（読み取り専用表示）。</summary>
		bool CanEdit { get; }

		/// <summary>
		/// 選択中ノードをまとめて動かす。ドラッグ中は呼ばれず、離した時に
		/// 合計移動量で1回だけ呼ばれる（Phase 3 で Undo を1操作にまとめるため）。
		/// </summary>
		void MoveNodes(IReadOnlyList<string> nodeIds, Vector2 graphDelta);

		/// <summary>この2つのポートを繋げるか。ドラッグ中のハイライト判定にも使う。</summary>
		bool CanConnect(FXCPortRef from, FXCPortRef to);

		void Connect(FXCPortRef from, FXCPortRef to);

		void DeleteNodes(IReadOnlyList<string> nodeIds);

		void DeleteEdges(IReadOnlyList<string> edgeIds);

		/// <summary>
		/// 右クリックメニューの中身をドメイン側が決める
		/// （追加できるノードの種類は AnimatorController 固有なので）。
		/// </summary>
		void PopulateContextMenu(GenericMenu menu, FXCGraphContext context);
	}
}
