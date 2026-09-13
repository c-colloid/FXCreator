using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator.UI
{
	public class ToggleinButton : Toggle
	{
		//bool _check;
	
		//public bool value {
		//	get => _check;
		//	set {
		//		if (_check == value)
		//		{
		//			return;
		//		}
			
		//		using (var pooled = ChangeEvent<bool>.GetPooled(_check, value))
		//		{
		//			pooled.target = this;
				
		//			SetValueWithoutNotify(value);
				
		//			SendEvent(pooled);
		//		}
		//	}
		//}
	
		public ToggleinButton()
		{
			var button = new Button();
			button.style.flexGrow = 1;
			button.text = this.text;
			Add(button);
			button.clicked += () => {
				value = !value;
			};
			this.Q<VisualElement>("unity-checkmark").parent.style.display = DisplayStyle.None;
		}
	
		public new class UxmlTraits : Toggle.UxmlTraits
		{
			private UxmlBoolAttributeDescription _enableRichText = new UxmlBoolAttributeDescription{name = "Enable Rich Text"};
			private UxmlBoolAttributeDescription _parseEscapeSequences = new UxmlBoolAttributeDescription{name = "Parse Escape Sequences"};
			private UxmlBoolAttributeDescription _displayTooltipWhenElided = new UxmlBoolAttributeDescription{name = "Display Tooltip When Elided"};
		
			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
			{
				base.Init(ve,bag,cc);
				var thisElement = (ToggleinButton)ve;
				var button = thisElement.Q<Button>();
				button.text = thisElement.text;
				button.enableRichText = thisElement.EnableRichText = _enableRichText.GetValueFromBag(bag,cc);
				button.parseEscapeSequences = thisElement.ParseEscapeSequences = _parseEscapeSequences.GetValueFromBag(bag,cc);
				button.displayTooltipWhenElided = thisElement.DisplayTooltipWhenElided = _displayTooltipWhenElided.GetValueFromBag(bag,cc);
				
				//button.style.borderBottomLeftRadius = thisElement.style.borderBottomLeftRadius;
				//button.style.borderBottomRightRadius = thisElement.style.borderBottomRightRadius;
				//button.style.borderTopLeftRadius = thisElement.style.borderTopLeftRadius;
				//button.style.borderTopRightRadius = thisElement.style.borderTopRightRadius;
			}
		}
		
		public void OnEnable()
		{
			var button = this.Q<Button>();
			button.style.borderBottomLeftRadius = this.style.borderBottomLeftRadius;
			button.style.borderBottomRightRadius = this.style.borderBottomRightRadius;
			button.style.borderTopLeftRadius = this.style.borderTopLeftRadius;
			button.style.borderTopRightRadius = this.style.borderTopRightRadius;
			
			button.style.backgroundImage = this.style.backgroundImage;
		}
	
	
		public new class UxmlFactory : UxmlFactory<ToggleinButton, UxmlTraits>{}
	
		//public void SetValueWithoutNotify(bool newValue)
		//{
		//	_check = newValue;
		//}
		
		public bool EnableRichText {get;set;}
		public bool ParseEscapeSequences {get;set;}
		public bool DisplayTooltipWhenElided {get;set;}
	}
}