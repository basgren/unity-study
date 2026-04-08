namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// Modal handle returned when barrel dragging starts. <c>DragAbility</c> flips
    /// <see cref="IsActive"/> to false from <c>StopDragging</c>, regardless of why
    /// the drag ended (button release, jump, lost ground). The resolver suspends
    /// all candidate evaluation while this handle is active.
    /// </summary>
    internal class BarrelDragHandle : IInteractionHandle {
        public bool IsActive { get; private set; } = true;

        public void Release() {
            IsActive = false;
        }
    }
}
