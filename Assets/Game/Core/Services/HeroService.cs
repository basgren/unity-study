using System;
using Game.Features.Characters.Hero;
using Game.Features.Characters.Hero.Interaction;
using Game.Features.Characters.Hero.ItemUse;
using UnityEngine;

namespace Game.Core.Services {
    /// <summary>
    /// Rendezvous point between the player instance and UI.
    /// The player registers itself here on spawn; HUD widgets discover it via events or direct access.
    /// </summary>
    public class HeroService : MonoBehaviour {
        public PlayerController Controller { get; private set; }
        public ItemUseService ItemUseService { get; private set; }
        public ItemUseService PerkUseService { get; private set; }
        public PlayerInteractionResolver Interaction { get; private set; }

        public event Action<PlayerController> OnHeroRegistered;
        public event Action OnHeroUnregistered;

        public void Register(
            PlayerController controller,
            ItemUseService itemUseService,
            ItemUseService perkUseService,
            PlayerInteractionResolver interaction
        ) {
            Controller = controller;
            ItemUseService = itemUseService;
            PerkUseService = perkUseService;
            Interaction = interaction;
            OnHeroRegistered?.Invoke(controller);
        }

        public void Unregister(PlayerController controller) {
            if (Controller != controller) {
                return;
            }

            Controller = null;
            ItemUseService = null;
            PerkUseService = null;
            Interaction = null;
            OnHeroUnregistered?.Invoke();
        }
    }
}
