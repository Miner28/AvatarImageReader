using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class CharField : TextInputFieldBase<char>
    {
        public CharField()
            : base(1, char.MinValue)
        {
            this.AddToClassList("UdonValueField");
        }

        public override char value
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
                this.value = this.text.ToCharArray()[0];
            }
            
        }

    }
}