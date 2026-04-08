using Game.Core.Bootstrap;
using Game.Features.Characters.Hero;
using Game.Features.Characters.Hero.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

namespace Game.UI.Widgets.InteractionHint {
    /// <summary>
    /// Bottom-center HUD caption that shows the current Interact binding label and
    /// a localized verb for the player's selected interaction target.
    ///
    /// Bound to <see cref="PlayerInteractionResolver.OnCurrentCandidateChanged"/>;
    /// hides when no candidate is selected, when the candidate has no action text,
    /// or when the hero is unregistered.
    ///
    /// <para><b>Prefab setup:</b> the script must live on a GameObject that stays
    /// active for the entire HUD lifetime (it subscribes to global events in
    /// OnEnable). Build a child GameObject that contains the visual panel
    /// (background + key label + action label) and assign it to <see cref="root"/>.
    /// The widget toggles that child to show/hide the hint — never the widget's
    /// own GameObject, because deactivating self would unsubscribe from the
    /// resolver and leave the widget permanently dead.</para>
    ///
    /// Expected hierarchy:
    /// <code>
    /// InteractionHint              &lt;-- InteractionHintWidget script lives here, always active
    ///   └ Panel                    &lt;-- assign to 'root'; toggled visible/hidden
    ///       ├ KeyLabel  (TMP)      &lt;-- assign to 'keyLabel'
    ///       └ ActionLabel (TMP)    &lt;-- assign to 'actionLabel'
    /// </code>
    /// </summary>
    public class InteractionHintWidget : MonoBehaviour {
        [Tooltip("Child GameObject that holds the visible hint panel. The widget toggles " +
                 "this object on/off, NOT its own GameObject. Must be a child — never " +
                 "assign the widget's own GameObject here, or the widget will deactivate " +
                 "itself on first hide and stop receiving resolver events.")]
        [SerializeField]
        private GameObject root;

        [SerializeField]
        private TextMeshProUGUI keyLabel;

        [SerializeField]
        private TextMeshProUGUI actionLabel;

        private PlayerInteractionResolver resolver;
        private LocalizedString boundActionText;

        private void OnEnable() {
            G.Hero.OnHeroRegistered += OnHeroRegistered;
            G.Hero.OnHeroUnregistered += OnHeroUnregistered;

            if (G.Input != null) {
                G.Input.OnSchemeChanged += OnSchemeChanged;
            }

            if (G.Hero.Controller != null) {
                BindResolver(G.Hero.Interaction);
            }

            // Always start hidden until something pushes a candidate.
            Show(false);
            RefreshKeyLabel();
        }

        private void OnDisable() {
            G.Hero.OnHeroRegistered -= OnHeroRegistered;
            G.Hero.OnHeroUnregistered -= OnHeroUnregistered;

            if (G.Input != null) {
                G.Input.OnSchemeChanged -= OnSchemeChanged;
            }

            UnbindResolver();
        }

        private void OnHeroRegistered(PlayerController controller) {
            BindResolver(G.Hero.Interaction);
            RefreshKeyLabel();
        }

        private void OnHeroUnregistered() {
            UnbindResolver();
            Show(false);
        }

        private void BindResolver(PlayerInteractionResolver newResolver) {
            UnbindResolver();

            resolver = newResolver;
            if (resolver == null) {
                return;
            }

            resolver.OnCurrentCandidateChanged += OnCurrentCandidateChanged;
            OnCurrentCandidateChanged(resolver.CurrentCandidate);
        }

        private void UnbindResolver() {
            UnbindActionText();

            if (resolver != null) {
                resolver.OnCurrentCandidateChanged -= OnCurrentCandidateChanged;
                resolver = null;
            }
        }

        private void OnCurrentCandidateChanged(IInteractionCandidate candidate) {
            UnbindActionText();

            if (candidate == null || candidate.ActionText == null || candidate.ActionText.IsEmpty) {
                Show(false);
                return;
            }

            boundActionText = candidate.ActionText;
            boundActionText.StringChanged += OnActionTextResolved;
            boundActionText.RefreshString();

            Show(true);
        }

        private void OnActionTextResolved(string value) {
            if (actionLabel != null) {
                actionLabel.text = value;
            }
        }

        private void UnbindActionText() {
            if (boundActionText != null) {
                boundActionText.StringChanged -= OnActionTextResolved;
                boundActionText = null;
            }
        }

        private void OnSchemeChanged() {
            RefreshKeyLabel();
        }

        private void RefreshKeyLabel() {
            if (keyLabel == null || G.Input == null) {
                return;
            }

            var interactAction = G.Input.Player.Interact;
            string display = interactAction.GetBindingDisplayString(
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames,
                G.Input.CurrentSchemeBindingGroup
            );

            keyLabel.text = display;
        }

        private void Show(bool visible) {
            // Toggle a child root rather than the widget itself — disabling our own
            // GameObject would fire OnDisable, unsubscribe from the resolver, and
            // leave the widget permanently dead.
            if (root != null) {
                root.SetActive(visible);
                return;
            }

            Debug.LogWarning(
                $"{nameof(InteractionHintWidget)}: 'root' is not assigned. " +
                "Hint cannot be shown or hidden. Wire it to a child GameObject.",
                this
            );
        }
    }
}
