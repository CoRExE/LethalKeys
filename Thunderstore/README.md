# LethalKeys

**Every time you die, one of your keybinds is randomly reassigned.**
Space now crouches, right click jumps, and your push-to-talk ended up on `N`. Good luck out there.

## How it works

- When **you** die, the mod picks one of your keys at random (keyboard or mouse button) and moves it to a random **free** key.
  Every action that used that key moves with it (e.g. `E` = interact + item use).
- Changes **stack** for the whole run. A key that has already moved can be drawn again.
- A message tells you what changed, once your HUD is visible again (after the end-of-day stats, quota and deadline screens).
- Everything goes back to normal when **the crew gets fired**.

### Conflict-free

- A key that is already used, by the game, by your own bindings or by another mod (InputUtils), is never assigned.
- **Escape, Enter and `/`** (menu and chat) never move.
- Your **real settings are never touched**: the scrambled keys are never written to your keybind settings, even if you
  rebind a key in the options menu. If you rebind a key the mod had moved, your choice wins.

### Saved with your run

- **Host**: the scrambled keys are stored in the game's save file. Reload the save and you get them back, with a summary on screen.
  Deleting the save deletes them too.
- **Client**: stored on your side, per host (Steam account). Rejoin the same host and you get them back.
- Like the rest of the game, progress is saved at the end of each day: quit mid-day and that day's deaths are forgotten.

## Only you are affected

LethalKeys is **client-side**: it only changes the keys of the player who dies. Other players don't need it
(but it's more fun if they have it too).

## Configuration

Open the in-game menu from [LethalConfig](https://thunderstore.io/c/lethal-company/p/AinaVT/LethalConfig/)
(installed automatically with the mod), or edit `BepInEx/config/lethalkeys.cfg`.

| Section | Content |
|---|---|
| Gameplay | Show on-screen messages |
| Actions | One checkbox per action that can lose its key (move, jump, sprint, crouch, interact, scan, use item...) |
| Touches - Lettres / Chiffres / Speciales / Souris | One checkbox per key that can be **given** to an action. Names follow your keyboard layout in the menu. |
| Touches - Autres | Extra keys, comma separated, Unity Input System names (QWERTY position): `numpad5, leftAlt, f1` |

## Known limitations

- Gamepad bindings are not affected.
- The scroll wheel (item switch) is never moved.

---

# LethalKeys (FR)

**À chaque mort, une de tes touches est réassignée au hasard.** Clavier et souris, sans conflit, et pour toute
la durée de la partie. Tout revient à la normale quand l'équipage est renvoyé.
Seul le joueur qui meurt est concerné. Les touches sont gardées avec la sauvegarde, et le menu en jeu passe par LethalConfig, installé automatiquement.

Source : [github.com/CoRExE/LethalKeys](https://github.com/CoRExE/LethalKeys)
