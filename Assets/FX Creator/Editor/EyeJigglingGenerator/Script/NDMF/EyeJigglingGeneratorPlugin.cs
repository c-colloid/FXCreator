#if NDMF
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using nadena.dev.ndmf;
using colloid.FXCreator.EyeJigglingGenerator;

[assembly: ExportsPlugin(typeof(EyeJigglingGeneratorPlugin))]
namespace colloid.FXCreator.EyeJigglingGenerator
{

public class EyeJigglingGeneratorPlugin : Plugin<EyeJigglingGeneratorPlugin>
{
	protected override void Configure()
	{
		// アニメーションの生成はMAの後に行う
		var Transforming = InPhase(BuildPhase.Transforming).AfterPlugin("nadena.dev.modular-avatar");
		Transforming.Run("Modify Animator", ctx => {
			var targetComponent = ctx.AvatarRootTransform.GetComponentInChildren<EyeJiggleSetting>();
			if (targetComponent == null || targetComponent.Mesh == null) return;
			
			int AnimlayerIndex = 0;
			for (int i = 0; i < ctx.AvatarDescriptor.baseAnimationLayers.Length; i++) {
				if (ctx.AvatarDescriptor.baseAnimationLayers[i].type != VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX) continue;
				AnimlayerIndex = i;
				if (ctx.AvatarDescriptor.baseAnimationLayers[i].isDefault)
				{
					ctx.AvatarDescriptor.baseAnimationLayers[i].isDefault = false;
					ctx.AvatarDescriptor.baseAnimationLayers[i].animatorController = null;
				}
			}
			var Animlayer = ctx.AvatarDescriptor.baseAnimationLayers[AnimlayerIndex];
			
			var targetAnimator =
				Animlayer.animatorController != null ?
				Object.Instantiate(Animlayer.animatorController) as AnimatorController :
				new AnimatorController(){name = "EyeJiggleFX"};
			var clip = targetComponent.GenerateClip();
			var layerIndex = targetComponent.LayerIndex;
			var writeDefault =
				Animlayer.animatorController != null && targetAnimator.layers.FirstOrDefault(o => o.stateMachine.defaultState != null) != null ?
				targetAnimator.layers.FirstOrDefault(o => o.stateMachine.defaultState != null).stateMachine.defaultState.writeDefaultValues :
				false;
			var namelistText = AssetDatabase.LoadAssetAtPath<TextAsset>(
				AssetDatabase.GUIDToAssetPath("46b34f15b82124242b6395d9a0d69f12"));
			var namesList = namelistText.text.Split(char.Parse("\n")).Where(o => o.IndexOf("//") < 0);
			
			var stateDefault = new AnimatorState
			{
				motion = clip,
				name = "EyeJiggling",
				writeDefaultValues = writeDefault
			};
			
			var stateStop = new AnimatorState
			{
				motion = new AnimationClip(),
				name = "StopEyeJiggling",
				writeDefaultValues = writeDefault
			};
			
			var stateMachine = new AnimatorStateMachine();
			stateMachine.AddState(stateDefault, stateMachine.entryPosition + Vector3.right*200);
			stateMachine.AddState(stateStop, stateMachine.entryPosition + Vector3.right*450);
			stateMachine.defaultState = stateDefault;
			
			foreach (var name in namesList)
			{
				if (!targetAnimator.parameters.Any(o => o.name == name)) continue;
				var transitionToStop = stateDefault.AddTransition(stateStop);
				transitionToStop.AddCondition(
					name.IndexOf("_DISABLE") < 0 ?
						AnimatorConditionMode.IfNot :
						AnimatorConditionMode.If,
					0,
					name);
				transitionToStop.duration = 0;
			}
			var transitionToDefault = stateStop.AddTransition(stateDefault);
			foreach (var name in namesList)
			{
				if (!targetAnimator.parameters.Any(o => o.name == name)) continue;
				transitionToDefault.AddCondition(
					name.IndexOf("_DISABLE") < 0 ? 
						AnimatorConditionMode.If :
						AnimatorConditionMode.IfNot,
					0,
					name);
			}
			transitionToDefault.duration = 0;
			
			var layer = new AnimatorControllerLayer
			{
				blendingMode = AnimatorLayerBlendingMode.Override,
				defaultWeight = 1,
				name = "EyeJigglingGen",
				stateMachine = stateMachine
			};
			
			var newAnimator = new AnimatorController();
			if (targetAnimator == null) targetAnimator = new AnimatorController();
			for (int i = 0; i < targetAnimator.layers.Length; i++) {
				if (i == layerIndex) newAnimator.AddLayer(layer);
				newAnimator.AddLayer(targetAnimator.layers[i]);
			}
			targetAnimator.layers = newAnimator.layers;
			if (!targetAnimator.layers.Any(o => o.name == layer.name)) targetAnimator.AddLayer(layer);

			ctx.AvatarDescriptor.baseAnimationLayers[AnimlayerIndex].animatorController = targetAnimator;
		});
		
		Transforming.Run("Remove Component", ctx => {
			Object.DestroyImmediate(ctx.AvatarRootTransform.GetComponentInChildren<EyeJiggleSetting>());
		});
	}
}
}
#endif