using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.UIElements;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class QuaternionField : BaseField<Quaternion>
    {
        public QuaternionField()
        {
            // Set up styling
            AddToClassList("UdonValueField");
            
            // Create Vector4 Editor and listen for changes
            Vector4Field field = new Vector4Field();
            field.OnValueChanged(
                e => 
                    value = new Quaternion(e.newValue.x, e.newValue.y, e.newValue.z, e.newValue.w)
                );
            Add(field);
        }

    }
}