using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.UIElements;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UIElements = UnityEditor.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class LayerMaskField : BaseField<LayerMask>
    {
        public LayerMaskField()
        {
            // Set up styling
            AddToClassList("UdonValueField");
            
            // Create LayerMask Editor and listen for changes
            UIElements.LayerMaskField field = new UIElements.LayerMaskField();
            field.OnValueChanged(e =>
            {
                this.value = e.newValue;
            });
            
            Add(field);
        }

    }
}