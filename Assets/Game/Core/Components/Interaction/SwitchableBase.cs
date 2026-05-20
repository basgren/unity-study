using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Core.Components.Interaction {
    /// <summary>
    /// Event invoked when a switchable state changes.
    /// The event argument is the new <c>IsActive</c> value.
    /// </summary>
    [System.Serializable]
    public class OnSwitchChangeEvent : UnityEvent<bool> {}

    /// <summary>
    /// Base behavior for objects that can be toggled by a <c>Switch</c>.
    /// </summary>
    /// <remarks>
    /// Unity serializes fields, not regular properties. Keep common behavior in this base class
    /// and store serialized backing fields in derived classes.
    /// </remarks>
    /// <example>
    /// <code>
    /// using UnityEngine;
    ///
    /// namespace Core.Components.Interaction {
    ///     public class Switchable : SwitchableBase {
    ///         [SerializeField] private bool isActive;
    ///         [SerializeField] private OnSwitchChangeEvent onChange = new();
    ///
    ///         protected override OnSwitchChangeEvent ChangeEvent => onChange;
    ///
    ///         public override bool IsActive {
    ///             get => isActive;
    ///             set => SetIsActive(ref isActive, value);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class SwitchableBase : MonoBehaviour {
        /// <summary>
        /// Current active state.
        /// Implementations typically back this with a serialized field.
        /// </summary>
        public abstract bool IsActive { get; set; }

        /// <summary>
        /// Event instance notified when <see cref="IsActive"/> changes.
        /// </summary>
        protected abstract OnSwitchChangeEvent ChangeEvent { get; }

        /// <summary>
        /// Code-side event raised when <see cref="IsActive"/> changes. Use this for runtime
        /// subscribers that prefer not to rely on inspector-wired UnityEvents.
        /// The argument is the new <c>IsActive</c> value.
        /// </summary>
        public event Action<bool> Changed;

        /// <summary>
        /// Shared setter helper for derived classes.
        /// Updates the backing field and invokes <see cref="ChangeEvent"/> only when value changes.
        /// </summary>
        /// <param name="field">Serialized backing field from a derived class.</param>
        /// <param name="value">New state value.</param>
        protected void SetIsActive(ref bool field, bool value) {
            if (field == value) {
                return;
            }

            field = value;
            ChangeEvent?.Invoke(field);
            Changed?.Invoke(field);
        }

        /// <summary>
        /// Flips <see cref="IsActive"/> between <c>true</c> and <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Exposed as a context-menu command so it can be triggered at runtime from the component
        /// header. Editing the serialized <c>IsActive</c> field directly in the Inspector bypasses
        /// the property setter and does not raise change events; use this instead to test.
        /// </remarks>
        [ContextMenu("Toggle")]
        public void Toggle() => IsActive = !IsActive;
    }
}
