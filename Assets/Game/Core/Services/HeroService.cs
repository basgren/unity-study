using System;
using Game.Features.Characters.Hero;
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

        public event Action<PlayerController> OnHeroRegistered;
        public event Action OnHeroUnregistered;

        public void Register(PlayerController controller, ItemUseService itemUseService) {
            Controller = controller;
            ItemUseService = itemUseService;
            OnHeroRegistered?.Invoke(controller);
        }

        public void Unregister(PlayerController controller) {
            if (Controller != controller) {
                return;
            }

            Controller = null;
            ItemUseService = null;
            OnHeroUnregistered?.Invoke();
        }
    }
}
