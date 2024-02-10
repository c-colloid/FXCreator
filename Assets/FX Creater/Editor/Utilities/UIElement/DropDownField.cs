using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace CustomUI
{
	public class DropDownField : PopupField<string>
{
	/// <summary>
    /// USS class name of elements of this type.
    /// </summary>
    public const string UssClassName = "unity-popup-field";
    
    
    /// <summary>
    /// Notify external subscribers that value of text property changed.
    /// </summary>
    public Action<string> OnValueChangedHandler;
    
    //readonly Label m_PopupLabel;
	public	List<string> Popupvalues{
		get => m_popupvalues;
		set {
			m_popupvalues = value;
		}
	}
	static	List<string> m_popupvalues = new List<string>(){"-select-"};
    
	public DropDownField(List<string> choices,int num) : base(choices,num)
    {
    	Popupvalues = choices;
    	AddToClassList(UssClassName);
   		this.RegisterValueChangedCallback(e => OnValueChangedHandler?.Invoke(e.newValue));
    }
    
	public DropDownField() : base(m_popupvalues,0)
    {
    	AddToClassList(UssClassName);
   		this.RegisterValueChangedCallback(e => OnValueChangedHandler?.Invoke(e.newValue));
    }
    
    
    [UsedImplicitly]
    public new class UxmlFactory : UxmlFactory<DropDownField, UxmlTraits> { }
    
    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        readonly UxmlIntAttributeDescription m_Index = new UxmlIntAttributeDescription { name = "index" };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            var field = (DropDownField)ve;
            field.index = m_Index.GetValueFromBag(bag, cc);
        }
    }
}
}
