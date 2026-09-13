using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.AnimatorGraph
{
	/// <summary>
	/// 1回の編集で何が起きたかの記録。Phase 7 で <c>IFxTarget.OnAfterEdit</c> に渡し、
	/// MA コンポーネントの同期やアセット参照の張り直しの判断材料にする。
	/// </summary>
	public sealed class AcEditReport
	{
		public AnimatorController Controller;
		public string OperationName;

		/// <summary>ノードや遷移が増減した（＝グラフの作り直しが要る）。</summary>
		public bool StructureChanged;

		/// <summary>パラメータ定義が変わった。</summary>
		public bool ParametersChanged;

		public readonly List<UnityEngine.Object> Created = new List<UnityEngine.Object>();
		public readonly List<UnityEngine.Object> Destroyed = new List<UnityEngine.Object>();
	}

	/// <summary>
	/// AnimatorController の編集トランザクション（Docs/FXCreator-Design.md §4.3）。
	///
	/// <c>UnityEditor.Animations</c> の API は Undo とサブアセット管理が壊れやすいので、
	/// 既知の落とし穴をこのクラス1箇所に封じ込める。
	///
	/// <list type="number">
	/// <item><c>layers</c> / <c>states</c> / <c>parameters</c> は<b>配列のコピー</b>を返す。
	/// 要素を書き換えても反映されず、配列ごと再代入が必要。</item>
	/// <item>State / StateMachine / Transition は Controller の<b>サブアセット</b>。
	/// Unity の Add 系は自動で <c>AddObjectToAsset</c> するが、Undo 登録は自前でやらないと
	/// Undo 後に迷子オブジェクトが残る。</item>
	/// <item>削除は <c>AssetDatabase.RemoveObjectFromAsset</c> ではなく
	/// <c>Undo.DestroyObjectImmediate</c>。</item>
	/// <item>1つのユーザー操作が複数の API 呼び出しになるので、Undo グループを明示的に畳む。</item>
	/// </list>
	///
	/// <example><code>
	/// using (var e = AcEdit.Begin(controller, "Add State"))
	/// {
	///     var state = e.AddState(stateMachine, "New State", position);
	///     e.SetMotion(state, clip);
	/// }
	/// </code></example>
	///
	/// 操作の途中で例外が出た場合は <c>Dispose</c> が
	/// <c>Undo.RevertAllDownToGroup</c> で巻き戻す（中途半端な Controller を残さない）。
	/// </summary>
	public sealed class AcEdit : IDisposable
	{
		/// <summary>編集が確定した直後に発火する。グラフはこれを見て作り直す。</summary>
		public static event Action<AcEditReport> AfterEdit;

		/// <summary>入れ子の <see cref="Begin"/>。内側はグループを共有し、畳むのは一番外だけ。</summary>
		[ThreadStatic] private static AcEdit _current;

		private readonly AnimatorController _controller;
		private readonly string _name;
		private readonly int _group;
		private readonly AcEdit _outer;
		private readonly AcEditReport _report;

		private bool _failed;
		private bool _disposed;

		private AcEdit(AnimatorController controller, string name, AcEdit outer)
		{
			_controller = controller;
			_name = name;
			_outer = outer;
			_report = outer != null ? outer._report : new AcEditReport
			{
				Controller = controller,
				OperationName = name
			};

			if (outer != null)
			{
				_group = outer._group;
				return;
			}

			Undo.IncrementCurrentGroup();
			Undo.SetCurrentGroupName(name);
			_group = Undo.GetCurrentGroup();
		}

		/// <summary>
		/// 編集を開始する。<paramref name="name"/> がそのまま Undo の操作名になる
		/// （Edit メニューに "Undo Add State" と出る）。
		/// </summary>
		public static AcEdit Begin(AnimatorController controller, string name)
		{
			if (controller == null)
			{
				throw new ArgumentNullException("controller");
			}
			AcEdit edit = new AcEdit(controller, name, _current);
			_current = edit;
			return edit;
		}

		public AnimatorController Controller => _controller;

		public AcEditReport Report => _report;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			_current = _outer;

			if (_outer != null)
			{
				// 内側のスコープ。失敗だけ外へ伝える。
				if (_failed)
				{
					_outer._failed = true;
				}
				return;
			}

			if (_failed)
			{
				Undo.RevertAllDownToGroup(_group);
				return;
			}

			Undo.CollapseUndoOperations(_group);
			if (_controller != null)
			{
				EditorUtility.SetDirty(_controller);
			}

			Action<AcEditReport> handler = AfterEdit;
			if (handler != null)
			{
				handler(_report);
			}
		}

		/// <summary>
		/// 操作本体を包む。例外が出たらトランザクションを失敗にしてから投げ直すので、
		/// <c>Dispose</c> がまとめて巻き戻せる。
		/// </summary>
		private T Guard<T>(Func<T> body)
		{
			try
			{
				return body();
			}
			catch
			{
				_failed = true;
				throw;
			}
		}

		private void Guard(Action body)
		{
			try
			{
				body();
			}
			catch
			{
				_failed = true;
				throw;
			}
		}

		#region Primitives

		/// <summary>
		/// このオブジェクトをこれから変更する、と Undo に伝える。
		/// <c>RecordObject</c> ではなく <c>RegisterCompleteObjectUndo</c> を使うのは、
		/// ステートマシンのように構造体配列を持つオブジェクトを丸ごと覚えさせるため。
		/// </summary>
		public void Record(UnityEngine.Object target)
		{
			if (target == null)
			{
				return;
			}
			Undo.RegisterCompleteObjectUndo(target, _name);
		}

		/// <summary>
		/// 「記録してから書き換える」だけの単純な編集。プロパティ1つごとに
		/// 専用メソッドを生やすより、インスペクタ側からはこれを呼ぶ方が素直
		/// （Undo の面倒は <see cref="Record"/> が見る）。
		/// </summary>
		public void Modify(UnityEngine.Object target, Action apply)
		{
			Guard(() =>
			{
				Record(target);
				apply();
			});
		}

		/// <summary>新しく作られたサブアセットを Undo に登録する（Undo 後の迷子を防ぐ）。</summary>
		private T RegisterCreated<T>(T created) where T : UnityEngine.Object
		{
			if (created == null)
			{
				return created;
			}
			Undo.RegisterCreatedObjectUndo(created, _name);
			_report.Created.Add(created);
			_report.StructureChanged = true;
			return created;
		}

		/// <summary>サブアセットを畳む。既に Unity 側が壊していれば何もしない。</summary>
		private void Destroy(UnityEngine.Object target)
		{
			if (target == null)
			{
				return;
			}
			_report.Destroyed.Add(target);
			_report.StructureChanged = true;
			Undo.DestroyObjectImmediate(target);
		}

		#endregion

		#region Structure: states and state machines

		public AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Vector2 position)
		{
			return Guard(() =>
			{
				Record(stateMachine);
				AnimatorState state = stateMachine.AddState(name, position);
				return RegisterCreated(state);
			});
		}

		public AnimatorStateMachine AddStateMachine(AnimatorStateMachine parent, string name, Vector2 position)
		{
			return Guard(() =>
			{
				Record(parent);
				AnimatorStateMachine child = parent.AddStateMachine(name, position);
				return RegisterCreated(child);
			});
		}

		/// <summary>
		/// State を消す。Unity の <c>RemoveState</c> は<b>この</b>ステートマシンが持つ
		/// 参照しか面倒を見ないので、先にレイヤー全体から自分宛ての遷移を掃除する。
		/// 残すと destinationState が null の遷移（＝グラフに描けない幽霊）になる。
		/// </summary>
		public void RemoveState(AnimatorStateMachine owner, AnimatorState state)
		{
			Guard(() =>
			{
				if (owner == null || state == null)
				{
					return;
				}

				PurgeReferencesTo(state);

				// 自分から出ている遷移も明示的に畳む（サブアセットを残さない）。
				Record(state);
				AnimatorStateTransition[] outgoing = state.transitions;
				state.transitions = new AnimatorStateTransition[0];
				for (int i = 0; i < outgoing.Length; i++)
				{
					Destroy(outgoing[i]);
				}

				Record(owner);
				owner.RemoveState(state);
				Destroy(state);
			});
		}

		public void RemoveStateMachine(AnimatorStateMachine parent, AnimatorStateMachine child)
		{
			Guard(() =>
			{
				if (parent == null || child == null)
				{
					return;
				}

				PurgeReferencesTo(child);

				// 配下を先に畳む。親を消してから辿ろうとしても手遅れになる。
				var doomed = new List<UnityEngine.Object>();
				CollectSubtree(child, doomed);

				Record(parent);
				parent.RemoveStateMachine(child);

				for (int i = 0; i < doomed.Count; i++)
				{
					Destroy(doomed[i]);
				}
				Destroy(child);
			});
		}

		#endregion

		#region State properties

		public void SetMotion(AnimatorState state, Motion motion)
		{
			Guard(() =>
			{
				Record(state);
				state.motion = motion;
			});
		}

		public void SetWriteDefaults(AnimatorState state, bool writeDefaults)
		{
			Guard(() =>
			{
				Record(state);
				state.writeDefaultValues = writeDefaults;
			});
		}

		public void SetSpeed(AnimatorState state, float speed)
		{
			Guard(() =>
			{
				Record(state);
				state.speed = speed;
			});
		}

		public void RenameState(AnimatorState state, string name)
		{
			Guard(() =>
			{
				Record(state);
				state.name = name;
			});
		}

		public void SetDefaultState(AnimatorStateMachine stateMachine, AnimatorState state)
		{
			Guard(() =>
			{
				Record(stateMachine);
				stateMachine.defaultState = state;
				_report.StructureChanged = true;
			});
		}

		#endregion

		#region Positions

		/// <summary>
		/// State の位置を書き戻す。<c>stateMachine.states</c> は<b>コピー</b>なので、
		/// 要素を書き換えたあと配列ごと戻さないと何も起きない（落とし穴1）。
		/// </summary>
		public void SetStatePosition(AnimatorStateMachine stateMachine, AnimatorState state, Vector2 position)
		{
			Guard(() =>
			{
				Record(stateMachine);
				ChildAnimatorState[] states = stateMachine.states;
				for (int i = 0; i < states.Length; i++)
				{
					if (states[i].state == state)
					{
						states[i].position = position;
						stateMachine.states = states;
						return;
					}
				}
			});
		}

		public void SetStateMachinePosition(AnimatorStateMachine parent, AnimatorStateMachine child, Vector2 position)
		{
			Guard(() =>
			{
				Record(parent);
				ChildAnimatorStateMachine[] children = parent.stateMachines;
				for (int i = 0; i < children.Length; i++)
				{
					if (children[i].stateMachine == child)
					{
						children[i].position = position;
						parent.stateMachines = children;
						return;
					}
				}
			});
		}

		/// <summary>Any / Entry / Exit / (Up) の位置。ステートマシン自身のフィールド（§4.1）。</summary>
		public void SetSpecialPosition(AnimatorStateMachine stateMachine, AcNodeKind kind, Vector2 position)
		{
			Guard(() =>
			{
				Record(stateMachine);
				switch (kind)
				{
					case AcNodeKind.Any:
						stateMachine.anyStatePosition = position;
						break;
					case AcNodeKind.Entry:
						stateMachine.entryPosition = position;
						break;
					case AcNodeKind.Exit:
						stateMachine.exitPosition = position;
						break;
					case AcNodeKind.Parent:
						stateMachine.parentStateMachinePosition = position;
						break;
				}
			});
		}

		#endregion

		#region Transitions

		public AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to)
		{
			return Guard(() =>
			{
				Record(from);
				return RegisterCreated(from.AddTransition(to));
			});
		}

		public AnimatorStateTransition AddTransition(AnimatorState from, AnimatorStateMachine to)
		{
			return Guard(() =>
			{
				Record(from);
				return RegisterCreated(from.AddTransition(to));
			});
		}

		public AnimatorStateTransition AddExitTransition(AnimatorState from)
		{
			return Guard(() =>
			{
				Record(from);
				return RegisterCreated(from.AddExitTransition());
			});
		}

		public AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState to)
		{
			return Guard(() =>
			{
				Record(stateMachine);
				return RegisterCreated(stateMachine.AddAnyStateTransition(to));
			});
		}

		public AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorStateMachine to)
		{
			return Guard(() =>
			{
				Record(stateMachine);
				return RegisterCreated(stateMachine.AddAnyStateTransition(to));
			});
		}

		public AnimatorTransition AddEntryTransition(AnimatorStateMachine stateMachine, AnimatorState to)
		{
			return Guard(() =>
			{
				Record(stateMachine);
				return RegisterCreated(stateMachine.AddEntryTransition(to));
			});
		}

		public AnimatorTransition AddEntryTransition(AnimatorStateMachine stateMachine, AnimatorStateMachine to)
		{
			return Guard(() =>
			{
				Record(stateMachine);
				return RegisterCreated(stateMachine.AddEntryTransition(to));
			});
		}

		public AnimatorTransition AddStateMachineTransition(
			AnimatorStateMachine parent, AnimatorStateMachine from, AnimatorState to)
		{
			return Guard(() =>
			{
				Record(parent);
				return RegisterCreated(parent.AddStateMachineTransition(from, to));
			});
		}

		public AnimatorTransition AddStateMachineTransition(
			AnimatorStateMachine parent, AnimatorStateMachine from, AnimatorStateMachine to)
		{
			return Guard(() =>
			{
				Record(parent);
				return RegisterCreated(parent.AddStateMachineTransition(from, to));
			});
		}

		public AnimatorTransition AddStateMachineExitTransition(AnimatorStateMachine parent, AnimatorStateMachine from)
		{
			return Guard(() =>
			{
				Record(parent);
				return RegisterCreated(parent.AddStateMachineExitTransition(from));
			});
		}

		/// <summary>
		/// 遷移を消す。所有者（State / Any / Entry / StateMachine遷移）は
		/// レイヤー全体を走査して突き止める。呼び出し側に覚えさせない。
		/// </summary>
		public void RemoveTransition(AnimatorTransitionBase transition)
		{
			Guard(() =>
			{
				if (transition == null)
				{
					return;
				}
				if (!TryDetachTransition(transition))
				{
					// 所有者が見つからなくても、サブアセットだけは残さない。
					Destroy(transition);
					return;
				}
				Destroy(transition);
			});
		}

		public void SetConditions(AnimatorTransitionBase transition, AnimatorCondition[] conditions)
		{
			Guard(() =>
			{
				Record(transition);
				transition.conditions = conditions;
			});
		}

		#endregion

		#region Traversal helpers

		/// <summary>
		/// Controller 内のどこかにある <paramref name="target"/> 宛ての遷移を全部消し、
		/// 既定ステート参照も外す。State も StateMachine も対象になる。
		/// </summary>
		private void PurgeReferencesTo(UnityEngine.Object target)
		{
			AnimatorControllerLayer[] layers = _controller.layers;
			for (int i = 0; i < layers.Length; i++)
			{
				PurgeIn(layers[i].stateMachine, target);
			}
		}

		private void PurgeIn(AnimatorStateMachine sm, UnityEngine.Object target)
		{
			if (sm == null)
			{
				return;
			}

			if ((UnityEngine.Object)sm.defaultState == target)
			{
				Record(sm);
				sm.defaultState = null;
			}

			RemoveMatching(sm.anyStateTransitions, target, t => { Record(sm); sm.RemoveAnyStateTransition((AnimatorStateTransition)t); });
			RemoveMatching(sm.entryTransitions, target, t => { Record(sm); sm.RemoveEntryTransition((AnimatorTransition)t); });

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}
				RemoveMatching(state.transitions, target, t => { Record(state); state.RemoveTransition((AnimatorStateTransition)t); });
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				AnimatorStateMachine source = child;
				RemoveMatching(
					sm.GetStateMachineTransitions(child),
					target,
					t => { Record(sm); sm.RemoveStateMachineTransition(source, (AnimatorTransition)t); });
				PurgeIn(child, target);
			}
		}

		private void RemoveMatching(
			AnimatorTransitionBase[] transitions,
			UnityEngine.Object target,
			Action<AnimatorTransitionBase> detach)
		{
			for (int i = 0; i < transitions.Length; i++)
			{
				AnimatorTransitionBase t = transitions[i];
				if (t == null)
				{
					continue;
				}
				if ((UnityEngine.Object)t.destinationState != target
					&& (UnityEngine.Object)t.destinationStateMachine != target)
				{
					continue;
				}
				detach(t);
				Destroy(t);
			}
		}

		/// <summary>所有者から遷移を外す。外せたら true。</summary>
		private bool TryDetachTransition(AnimatorTransitionBase transition)
		{
			AnimatorControllerLayer[] layers = _controller.layers;
			for (int i = 0; i < layers.Length; i++)
			{
				if (TryDetachIn(layers[i].stateMachine, transition))
				{
					return true;
				}
			}
			return false;
		}

		private bool TryDetachIn(AnimatorStateMachine sm, AnimatorTransitionBase transition)
		{
			if (sm == null)
			{
				return false;
			}

			var asStateTransition = transition as AnimatorStateTransition;
			if (asStateTransition != null && Contains(sm.anyStateTransitions, transition))
			{
				Record(sm);
				sm.RemoveAnyStateTransition(asStateTransition);
				return true;
			}

			var asPlainTransition = transition as AnimatorTransition;
			if (asPlainTransition != null && Contains(sm.entryTransitions, transition))
			{
				Record(sm);
				sm.RemoveEntryTransition(asPlainTransition);
				return true;
			}

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null || asStateTransition == null || !Contains(state.transitions, transition))
				{
					continue;
				}
				Record(state);
				state.RemoveTransition(asStateTransition);
				return true;
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				if (asPlainTransition != null && Contains(sm.GetStateMachineTransitions(child), transition))
				{
					Record(sm);
					sm.RemoveStateMachineTransition(child, asPlainTransition);
					return true;
				}
				if (TryDetachIn(child, transition))
				{
					return true;
				}
			}

			return false;
		}

		private static bool Contains(AnimatorTransitionBase[] transitions, AnimatorTransitionBase target)
		{
			for (int i = 0; i < transitions.Length; i++)
			{
				if (transitions[i] == target)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary><paramref name="sm"/> 配下のサブアセットを（sm 自身を除いて）集める。</summary>
		private static void CollectSubtree(AnimatorStateMachine sm, List<UnityEngine.Object> sink)
		{
			if (sm == null)
			{
				return;
			}

			sink.AddRange(sm.anyStateTransitions);
			sink.AddRange(sm.entryTransitions);

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}
				sink.AddRange(state.transitions);
				sink.Add(state);
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				sink.AddRange(sm.GetStateMachineTransitions(child));
				CollectSubtree(child, sink);
				sink.Add(child);
			}
		}

		#endregion
	}
}
