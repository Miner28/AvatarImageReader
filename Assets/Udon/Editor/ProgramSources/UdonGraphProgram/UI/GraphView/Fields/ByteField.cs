using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class ByteField : TextInputFieldBase<byte>
    {
        public ByteField()
            : base(3, char.MinValue)
        {
            this.AddToClassList("UdonValueField");
            this.isDelayed = true;
        }

        public override byte value
        {
            get
            {
                return base.value;
            }
            set
            {
                base.value = value;
                this.text = value.ToString();
            }
        }

        protected override void ExecuteDefaultAction(EventBase evt)
        {
            base.ExecuteDefaultAction(evt);
            
            if (this.text.Length > 0)
            {
                try
                {
                    
                    var byteValue = Convert.ToByte(this.text);
                    this.value = byteValue;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }
            
        }

    }
}