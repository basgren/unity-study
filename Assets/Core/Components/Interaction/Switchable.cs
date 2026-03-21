using UnityEngine;

namespace Core.Components.Interaction {
    public class Switchable : SwitchableBase {
        [SerializeField]
        private bool isActive;

        [SerializeField]
        private OnSwitchChangeEvent onChange = new();

        protected override OnSwitchChangeEvent ChangeEvent => onChange;

        public override bool IsActive {
            get => isActive;
            set => SetIsActive(ref isActive, value);
        }
    }
}
