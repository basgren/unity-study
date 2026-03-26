# AGENTS.md

## Project Context
Unity 2D pixel art platformer.
For full project layout and placement rules see `Assets/Game/Docs/PROJECT_STRUCTURE.md`.

Main priorities:
1. Do not break gameplay
2. Do not break serialized data, prefabs, scenes, or inspector wiring
3. Preserve pixel-perfect visuals
4. Keep changes small, local, and maintainable

## Coding Rules
- Use C#
- Use K&R brace style
- Always use braces for all conditionals and loops
- Write all comments in English
- Prefer explicit, readable code over clever abstractions
- Keep methods focused
- Avoid magic numbers where practical
- Do not reformat unrelated code
- Add XML documentation to important classes and their public API methods when it adds clarity; skip trivial or self-explanatory members
- Add brief inline comments for non-obvious solutions (workarounds, timing tricks, subtle interactions); skip comments for self-evident code

Example:
```csharp
public void SetDir(int dir) {
    if (dir == 0) {
        return;
    }

    currentDir = dir;
}

```

## Unity Rules
- Prefer `[SerializeField] private` fields over public fields
- Do not rename serialized fields, animator parameters, assets, or classes without a good reason
- Do not break prefab references, scene references, animation events, or inspector bindings
- Avoid `FindObjectOfType`, `GameObject.Find`, and similar global lookups in runtime code
- Cache references when needed
- Avoid unnecessary allocations in `Update`, `LateUpdate`, and `FixedUpdate`
- Use `FixedUpdate` for physics-related movement
- Use `Update` for input unless the existing system already uses another pattern
- Follow existing project conventions before introducing new structure

## Pixel Art Rules
- Preserve pixel-perfect presentation
- Do not introduce blur, filtering, subpixel wobble, or scaling that hurts readability
- Be careful when changing camera, UI, sprites, fonts, TextMeshPro, animation timing, or Pixel Perfect settings

## Gameplay Rules
- Do not silently change movement feel unless required
- Do not silently change speed, gravity, jump timing, hitboxes, knockback, attack timing, or collision behavior
- Prefer explicit and readable state transitions
- Keep movement, collision, damage, and animation logic reasonably separated
- Expose tuning values in the Inspector when they are meant to be adjusted

## Service Configuration Rules
- Services exposed via the `G` global static class must NOT use `[SerializeField]` for their configuration references (they are created dynamically, so serialized fields would be unset)
- Instead, put shared references (AudioMixer, mixer groups, prefabs, etc.) on `MainConfig` ScriptableObject (`Resources/Configs/MainConfig`) and access them via `G.Config` in an `Init()` method called from `GInit`

## Architecture Rules
Prefer:
- small local components
- explicit dependencies
- composition over unnecessary inheritance
- simple and debuggable logic

Avoid:
- speculative abstractions
- giant manager classes
- hidden cross-system side effects
- broad rewrites
- new packages or frameworks unless clearly needed

## Editing Discipline
Before changing code:
- read surrounding code
- check inspector, prefab, scene, and animation implications
- understand whether the script is gameplay-critical, editor-only, or presentation-only

When changing code:
- touch only relevant files
- keep diffs small
- for moving files and renaming use `git mv` to keep history
- when moving a file or folder, always move its `.meta` file together (e.g.,
  `git mv Foo.cs dest/Foo.cs && git mv Foo.cs.meta dest/Foo.cs.meta`). Unity tracks assets by
  GUID stored in `.meta` files — if a `.meta` is left behind or regenerated, all references break
- do not do unrelated cleanup
- do not rename things casually
- do not move files unless required

## Risk Areas
Be extra careful with:
- serialized fields
- prefab variants
- ScriptableObject references
- animation events
- Animator parameter names
- Input System references
- Rigidbody2D movement
- collision callbacks
- scene transitions
- global services / singletons
- editor scripts

## Default Rule
If the task is unclear, make the smallest safe change and preserve current behavior.

## Expected Agent Output
After finishing, report:
- what changed
- why it changed
- assumptions made
- any Unity Editor steps required
- any risks or follow-up items
