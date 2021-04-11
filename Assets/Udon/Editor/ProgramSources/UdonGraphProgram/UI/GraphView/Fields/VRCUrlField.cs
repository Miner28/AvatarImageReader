using UnityEngine.Experimental.UIElements;
using VRC.SDKBase;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class VRCUrlField : TextInputFieldBase<VRCUrl>
    {
        public VRCUrlField()
            : base(-1, char.MinValue)
        {
            AddToClassList("UdonValueField");
            RegisterCallback<BlurEvent>(OnBlur);
        }

        private void OnBlur(BlurEvent evt)
        {
            base.value = new VRCUrl(text);
        }

        public override VRCUrl value
        {
            get => base.value;
            set
            {
                base.value = value;
                text = value?.Get() ?? string.Empty;
            }
        }

        ~VRCUrlField()
        {
            UnregisterCallback<BlurEvent>(OnBlur);
        }
    }
}
