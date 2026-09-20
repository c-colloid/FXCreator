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

		/// <summary>
		/// このトランザクションで起きた改名と型変更。
		///
		/// 「パラメータが変わった」だけでは、同期設定（Expression Parameters /
		/// MA Parameters）を追随させられない。名前が変わったのか増えたのかが
		/// 分からないと、追随側は<b>古い名前の行を残したまま新しい行を足す</b>ことになり、
		/// 名前が一致しなくなって VRChat で動かなくなる（§6.2）。
		/// </summary>
		public readonly List<AcParameterRename> ParameterRenames = new List<AcParameterRename>();

		public readonly List<AcParameterRetype> ParameterRetypes = new List<AcParameterRetype>();
	}

	public struct AcParameterRename
	{
		public string OldName;
		public string NewName;
	}

	public struct AcParameterRetype
	{
		public string Name;
		public AnimatorControllerParameterType From;
		public AnimatorControllerParameterType To;
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

		#region Parameters

		public AnimatorControllerParameter AddParameter(string name, AnimatorControllerParameterType type)
		{
			return Guard(() =>
			{
				Record(_controller);
				string unique = UniqueParameterName(name);
				_controller.AddParameter(unique, type);
				_report.ParametersChanged = true;

				AnimatorControllerParameter[] parameters = _controller.parameters;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (string.Equals(parameters[i].name, unique, StringComparison.Ordinal))
					{
						return parameters[i];
					}
				}
				return null;
			});
		}

		public void RemoveParameter(string name)
		{
			Guard(() =>
			{
				Record(_controller);
				AnimatorControllerParameter[] parameters = _controller.parameters;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (!string.Equals(parameters[i].name, name, StringComparison.Ordinal))
					{
						continue;
					}
					_controller.RemoveParameter(i);
					_report.ParametersChanged = true;
					return;
				}
			});
		}

		/// <summary>
		/// パラメータの値を書き換える。<c>controller.parameters</c> は配列のコピーなので、
		/// 要素を触っただけでは反映されない（落とし穴1）。配列ごと戻す。
		/// </summary>
		public void ModifyParameter(string name, Action<AnimatorControllerParameter> apply)
		{
			Guard(() =>
			{
				Record(_controller);
				AnimatorControllerParameter[] parameters = _controller.parameters;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (!string.Equals(parameters[i].name, name, StringComparison.Ordinal))
					{
						continue;
					}
					apply(parameters[i]);
					_controller.parameters = parameters;
					_report.ParametersChanged = true;
					return;
				}
			});
		}

		/// <summary>
		/// パラメータの型を変え、<b>条件を新しい型へ読み替える</b>（§6.1）。
		///
		/// 型だけ差し替えると、Bool 用の <c>If</c> が Int のパラメータに付いたままになる。
		/// Unity はそれを弾かないので、見た目は正常なのに遷移しない Controller ができる。
		/// 何をどう読み替えるかは <see cref="AcParameterTypeChange"/> が決める
		/// （Controller を読むだけの純粋関数なので、呼ぶ前に確認ダイアログへ出せる）。
		///
		/// 読み替えられない参照（Mirror に Int を挿す等）は<b>そのまま残す</b>。
		/// 黙って消すと、型を戻しても元に戻らない。
		/// </summary>
		public void ChangeParameterType(string name, AnimatorControllerParameterType newType)
		{
			Guard(() =>
			{
				AcParameterTypeChangePlan plan = AcParameterTypeChange.Plan(_controller, name, newType);
				if (plan.IsNoOp)
				{
					return;
				}

				Record(_controller);
				AnimatorControllerParameter[] parameters = _controller.parameters;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (!string.Equals(parameters[i].name, name, StringComparison.Ordinal))
					{
						continue;
					}
					CarryDefault(parameters[i], plan.From, newType);
					parameters[i].type = newType;
					break;
				}
				// 配列のコピーなので戻さないと反映されない（§4.3 の罠1）。
				_controller.parameters = parameters;
				_report.ParametersChanged = true;
				_report.ParameterRetypes.Add(
					new AcParameterRetype { Name = name, From = plan.From, To = newType });

				// 条件の読み替え。遷移ごとにまとめて1回だけ書き戻す
				// （conditions も配列のコピーなので、1本ずつ戻すと最後の1本しか残らない）。
				var byTransition = new Dictionary<AnimatorTransitionBase, List<AcConditionRewrite>>();
				for (int i = 0; i < plan.Rewrites.Count; i++)
				{
					AcConditionRewrite rewrite = plan.Rewrites[i];
					if (rewrite.Transition == null)
					{
						continue;
					}
					List<AcConditionRewrite> list;
					if (!byTransition.TryGetValue(rewrite.Transition, out list))
					{
						list = new List<AcConditionRewrite>();
						byTransition.Add(rewrite.Transition, list);
					}
					list.Add(rewrite);
				}

				foreach (KeyValuePair<AnimatorTransitionBase, List<AcConditionRewrite>> pair in byTransition)
				{
					AnimatorCondition[] conditions = pair.Key.conditions;
					bool touched = false;
					for (int i = 0; i < pair.Value.Count; i++)
					{
						AcConditionRewrite rewrite = pair.Value[i];
						if (rewrite.Index < 0 || rewrite.Index >= conditions.Length)
						{
							continue;
						}
						conditions[rewrite.Index].mode = rewrite.ToMode;
						conditions[rewrite.Index].threshold = rewrite.ToThreshold;
						touched = true;
					}
					if (touched)
					{
						Record(pair.Key);
						pair.Key.conditions = conditions;
						// 条件が変われば toggle / switch の畳み込みも変わる（§4.2）。
						_report.StructureChanged = true;
					}
				}
			});
		}

		/// <summary>
		/// 既定値を新しい型へ持ち越す。
		/// <c>AnimatorControllerParameter</c> は Bool / Int / Float の値を別々に持つので、
		/// 型だけ変えると「true だった Bool が 0 の Int になる」といった取りこぼしが出る。
		/// </summary>
		private static void CarryDefault(
			AnimatorControllerParameter parameter,
			AnimatorControllerParameterType from,
			AnimatorControllerParameterType to)
		{
			float value;
			switch (from)
			{
				case AnimatorControllerParameterType.Bool:
					value = parameter.defaultBool ? 1f : 0f;
					break;
				case AnimatorControllerParameterType.Int:
					value = parameter.defaultInt;
					break;
				case AnimatorControllerParameterType.Float:
					value = parameter.defaultFloat;
					break;
				default:
					// Trigger は値を持たない。
					value = 0f;
					break;
			}

			switch (to)
			{
				case AnimatorControllerParameterType.Bool:
					parameter.defaultBool = Mathf.Abs(value) > 0.0001f;
					break;
				case AnimatorControllerParameterType.Int:
					parameter.defaultInt = Mathf.RoundToInt(value);
					break;
				case AnimatorControllerParameterType.Float:
					parameter.defaultFloat = value;
					break;
			}
		}

		/// <summary>
		/// パラメータを改名し、<b>Controller 内のすべての参照を追随させる</b>（§6.1）。
		///
		/// 追随先は条件式だけではない。取りこぼすと、名前だけ変わって挙動が壊れた
		/// Controller ができあがる:
		/// <list type="bullet">
		/// <item>遷移の <c>AnimatorCondition.parameter</c>（State / Any / Entry / SubSM の全部）</item>
		/// <item>State の speed / cycleOffset / mirror / timeParameter</item>
		/// <item>BlendTree の blendParameter / blendParameterY と、子モーションの directBlendParameter</item>
		/// </list>
		/// </summary>
		public void RenameParameter(string oldName, string newName)
		{
			Guard(() =>
			{
				if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || oldName == newName)
				{
					return;
				}

				string unique = UniqueParameterName(newName);

				Record(_controller);
				AnimatorControllerParameter[] parameters = _controller.parameters;
				bool found = false;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (string.Equals(parameters[i].name, oldName, StringComparison.Ordinal))
					{
						parameters[i].name = unique;
						found = true;
						break;
					}
				}
				if (!found)
				{
					return;
				}
				_controller.parameters = parameters;
				_report.ParametersChanged = true;
				// 同期設定の追随に「何が何へ変わったか」が要る（§6.2）。
				_report.ParameterRenames.Add(
					new AcParameterRename { OldName = oldName, NewName = unique });

				AnimatorControllerLayer[] layers = _controller.layers;
				for (int i = 0; i < layers.Length; i++)
				{
					RenameInStateMachine(layers[i].stateMachine, oldName, unique);
				}
			});
		}

		private void RenameInStateMachine(AnimatorStateMachine sm, string oldName, string newName)
		{
			if (sm == null)
			{
				return;
			}

			RenameInTransitions(sm, sm.anyStateTransitions, oldName, newName);
			RenameInTransitions(sm, sm.entryTransitions, oldName, newName);

			ChildAnimatorState[] states = sm.states;
			for (int i = 0; i < states.Length; i++)
			{
				AnimatorState state = states[i].state;
				if (state == null)
				{
					continue;
				}

				RenameInTransitions(state, state.transitions, oldName, newName);

				if (state.speedParameter == oldName
					|| state.cycleOffsetParameter == oldName
					|| state.mirrorParameter == oldName
					|| state.timeParameter == oldName)
				{
					Record(state);
					if (state.speedParameter == oldName) state.speedParameter = newName;
					if (state.cycleOffsetParameter == oldName) state.cycleOffsetParameter = newName;
					if (state.mirrorParameter == oldName) state.mirrorParameter = newName;
					if (state.timeParameter == oldName) state.timeParameter = newName;
				}

				RenameInMotion(state.motion, oldName, newName);
			}

			ChildAnimatorStateMachine[] children = sm.stateMachines;
			for (int i = 0; i < children.Length; i++)
			{
				AnimatorStateMachine child = children[i].stateMachine;
				if (child == null)
				{
					continue;
				}
				RenameInTransitions(sm, sm.GetStateMachineTransitions(child), oldName, newName);
				RenameInStateMachine(child, oldName, newName);
			}
		}

		private void RenameInTransitions(
			UnityEngine.Object owner, AnimatorTransitionBase[] transitions, string oldName, string newName)
		{
			for (int i = 0; i < transitions.Length; i++)
			{
				AnimatorTransitionBase transition = transitions[i];
				if (transition == null)
				{
					continue;
				}

				AnimatorCondition[] conditions = transition.conditions;
				bool touched = false;
				for (int c = 0; c < conditions.Length; c++)
				{
					if (!string.Equals(conditions[c].parameter, oldName, StringComparison.Ordinal))
					{
						continue;
					}
					conditions[c].parameter = newName;
					touched = true;
				}
				if (touched)
				{
					// conditions も配列のコピー。戻さないと何も起きない。
					Record(transition);
					transition.conditions = conditions;
				}
			}
		}

		/// <summary>BlendTree は入れ子になりうるので再帰で辿る。</summary>
		private void RenameInMotion(Motion motion, string oldName, string newName)
		{
			var tree = motion as BlendTree;
			if (tree == null)
			{
				return;
			}

			if (tree.blendParameter == oldName || tree.blendParameterY == oldName)
			{
				Record(tree);
				if (tree.blendParameter == oldName) tree.blendParameter = newName;
				if (tree.blendParameterY == oldName) tree.blendParameterY = newName;
			}

			ChildMotion[] children = tree.children;
			bool touched = false;
			for (int i = 0; i < children.Length; i++)
			{
				if (string.Equals(children[i].directBlendParameter, oldName, StringComparison.Ordinal))
				{
					children[i].directBlendParameter = newName;
					touched = true;
				}
				RenameInMotion(children[i].motion, oldName, newName);
			}
			if (touched)
			{
				Record(tree);
				tree.children = children;
			}
		}

		/// <summary>同名があると Unity 側が黙って番号を足すので、こちらで先に決めておく。</summary>
		private string UniqueParameterName(string desired)
		{
			if (string.IsNullOrEmpty(desired))
			{
				desired = "New Parameter";
			}

			AnimatorControllerParameter[] parameters = _controller.parameters;
			bool Taken(string candidate)
			{
				for (int i = 0; i < parameters.Length; i++)
				{
					if (string.Equals(parameters[i].name, candidate, StringComparison.Ordinal))
					{
						return true;
					}
				}
				return false;
			}

			if (!Taken(desired))
			{
				return desired;
			}
			for (int n = 1; n < 1000; n++)
			{
				string candidate = desired + " " + n;
				if (!Taken(candidate))
				{
					return candidate;
				}
			}
			return desired;
		}

		#endregion
	}
}
