# LethalKeys : conception

> Mod Lethal Company : **chaque fois que tu meurs, une de tes touches est réassignée au hasard.**
> Les changements s'accumulent jusqu'à la fin de la partie.

## 1. Règles du jeu

| Règle | Choix |
|---|---|
| Qui est touché | Seulement le joueur qui meurt |
| Ce qui change | Une **touche** utilisée, tirée au hasard, est remplacée par une touche aléatoire libre, pour toutes les actions qui l'utilisent (voir §1 bis) |
| Conflits | On les **minimise** (règles détaillées en §4, à affiner plus tard) |
| Cumul | Oui : 3 morts = 3 touches modifiées (une même action peut être retirée au sort) |
| Retour à la normale | À la **fin de partie** : équipage renvoyé (game over) ou sortie du lobby |
| Information du joueur | Message à l'écran : *« Sauter → P »* |

Conséquence technique importante : comme seul le joueur mort est touché, le mod est **100 % côté client**.
Pas de réseau, et les autres joueurs n'ont pas besoin de l'installer.

## 1 bis. Actions du jeu (relevé du 20/11/2026, jeu vanilla)

Dump de l'étape 2, asset `PlayerActions`, map `Movement`. Aucun binding n'a de groupe
(`groups` vide) : on reconnaît le clavier par le préfixe `<Keyboard>/`.

| Statut | Actions (touche par défaut) |
|---|---|
| **Éligibles** | Move haut/bas/gauche/droite (W S A D), Jump (Espace), Sprint (Maj), Crouch (Ctrl), Interact (E), Discard (G), ItemSecondaryUse (Q), ReloadBatteries (R), InspectItem (Z), VoiceButton (T), Emote1/2 (1, 2), BuildMode (B), ConfirmBuildMode (V), Delete (X), UseUtilitySlot (Tab) |
| **Exclues : souris** | Look, Use, ActivateItem (clic G), PingScan (clic D), SwitchItem (molette) |
| **Exclues : verrouillage** | OpenMenu (Échap), EnableChat (/), SubmitChat (Entrée) |
| **Exclues : debug** | SpeedCheat (H), SetFreeCamera (C), mais leurs touches restent « occupées » |

### Constats qui changent la conception

1. **Touches partagées.** E sert à `Interact`, `ItemTertiaryUse` et `QEItemInteract/positive`.
   Q sert à `ItemSecondaryUse` et `QEItemInteract/negative`. Si on ne modifie que `Interact`,
   l'objet tenu continue de réagir à E.
   → **On tire au sort une touche, pas une action** : on prend une touche utilisée, et
   *tous* les bindings clavier qui l'utilisent passent ensemble sur la nouvelle touche.
   Les groupes restent cohérents et il n'y a pas de cas particulier à écrire.
2. **Chemins physiques.** `<Keyboard>/w` désigne la *position* de la touche W sur un clavier
   QWERTY, c'est-à-dire Z sur AZERTY. Pour le message à l'écran, on utilise donc
   `InputControl.displayName` ou `GetBindingDisplayString()`, jamais le chemin brut.
3. **Manette.** Les bindings `<Gamepad>/…` ne sont jamais touchés.
4. **Mods tiers.** Avec InputUtils, d'autres mods ajoutent leurs propres actions dans d'autres
   assets (ReservedItemSlot, TooManyEmotes…). Il faudra les compter comme touches occupées
   (à traiter plus tard, voir §4).

5. **Deux assets.** Le joueur lit `InputSystem.actions` (`PlayerActions`). Les réglages ont une
   copie `PlayerActions(Clone)`. **Les touches personnalisées du joueur sont dans l'asset global**
   (exemple observé : Crouch → C, VoiceButton → bouton souris avant), et la copie n'en a aucune.
   → Le mod modifie les deux, mais calcule les touches occupées à partir de l'asset global
   (`effectivePath`).

Touches clavier occupées en vanilla : `w s a d space shift ctrl e q g r z t 1 2 b v x tab c h escape slash enter`.

## 2. Pile technique

- **BepInEx 5** (chargeur de mods) + **HarmonyX** (patch des méthodes du jeu).
- **C# / netstandard2.1**, comme tous les mods LC (le jeu tourne sous Unity 2022.3).
- **Unity Input System** : le jeu utilise des `InputAction`. On les réassigne avec
  `ApplyBindingOverride`, sans toucher aux réglages enregistrés du joueur.
- **Dépendance optionnelle** : InputUtils / LC ControlTipKeys pour que les indications à l'écran
  (« Lâcher l'objet : [G] ») affichent la nouvelle touche.

## 3. Architecture

```
LethalKeys/
├─ LethalKeys.csproj
├─ Plugin.cs              point d'entrée BepInEx, config, Harmony.PatchAll()
├─ KeyScrambler.cs        cœur : choix de l'action + de la touche, application, reset
├─ KeyPool.cs             touches autorisées et logique anti-conflit
├─ Patches/
│  ├─ DeathPatch.cs       PlayerControllerB.KillPlayer   → déclenche un tirage
│  ├─ ResetPatch.cs       fin de partie / déconnexion     → annule tout
│  └─ SettingsPatch.cs    protège la sauvegarde des touches du joueur
└─ CONCEPTION.md
```

### Flux

```
Mort du joueur local (KillPlayer, postfix, si __instance == joueur local)
  └─ KeyScrambler.Scramble()
       1. tirer une action éligible (Jump, Crouch, Sprint, Interact, Discard…)
       2. si composite (Move = Z/Q/S/D) → tirer une de ses 4 directions
       3. tirer une touche dans KeyPool (moins les touches déjà prises)
       4. action.ApplyBindingOverride(index, "<Keyboard>/p")
       5. mémoriser le changement (liste en mémoire)
       6. afficher le message (HUDManager.DisplayTip) : tout de suite ou à la réapparition

Fin de partie / déconnexion
  └─ KeyScrambler.ResetAll() → RemoveBindingOverride sur chaque changement mémorisé,
                                puis on restaure les réglages personnels du joueur
```

### Points d'accroche dans le jeu (existence vérifiée dans Assembly-CSharp)

Autres méthodes utiles repérées : `IngamePlayerSettings.SaveSettingsToPrefs`, `ResetAllKeybinds`,
`DiscardChangedSettings`, `StartOfRound.ReviveDeadPlayers`, `StartOfRound.playersFiredGameOver`.

| Événement | Méthode visée |
|---|---|
| Mort | `GameNetcodeStuff.PlayerControllerB.KillPlayer` |
| Game over | `StartOfRound.ResetShip` / `StartOfRound.FirePlayersAfterDeadlineClientRpc` |
| Sortie du lobby | `GameNetworkManager.Disconnect` / `StartOfRound.OnDestroy` |
| Accès aux actions | `IngamePlayerSettings.Instance.playerInput.actions` |
| Sauvegarde des touches | `IngamePlayerSettings.SaveChangedSettings` |

## 4. Anti-conflits (version 1, à affiner)

1. **Pool de touches** : lettres, chiffres et quelques touches simples (F, G, P, ;…).
   **Exclues** : Échap, Entrée, Windows, Alt, F1–F12, Impr. écran.
2. **Pas de doublon** : on retire du pool toutes les touches déjà utilisées par une action
   (réglages du joueur + changements précédents du mod).
3. **Actions jamais touchées** : Menu (Échap), Chat / valider le chat, souris (regard, clic).
4. Si le pool est vide → aucun changement, avec un message dans le log.

Pistes à discuter plus tard : préférer les touches proches de la main gauche, faire plus de
tirages sur les actions vitales (Sprint, Jump), limiter le nombre de mouvements touchés…

## 5. Configuration (BepInEx `.cfg` + menu LethalConfig optionnel)

| Section | Contenu |
|---|---|
| `Gameplay` | `ShowTip` : messages à l'écran (changements + résumé au chargement) |
| `Actions` | une case par action pouvant perdre sa touche (19 actions, toutes cochées par défaut) |
| `Touches - Lettres / Chiffres / Speciales / Souris` | une case par touche pouvant être attribuée (clé = nom QWERTY, libellé du menu = nom sur le clavier du joueur) |
| `Touches - Autres` | `ExtraKeys` : touches en plus, en texte court (`numpad5, leftAlt, f1`) |
| `Debug` | `DumpBindings`, `DebugKeys` |

Les listes sont des cases à cocher et non du texte : le champ texte de LethalConfig 1.4.6 ignore
`CharacterLimit` et tronque à environ 120 caractères. Les anciennes options `EligibleActions` et `AllowedKeys` sont
retirées automatiquement du `.cfg`. Les messages du HUD et les textes du menu n'ont pas d'accents.

## 6. Risques connus

- **Sauvegarde polluée** : si le joueur ouvre ses réglages puis valide, le jeu enregistre
  *tous* les overrides, y compris les nôtres. → `SettingsPatch` retire nos overrides avant
  la sauvegarde et les réapplique après.
- **Crash ou Alt+F4** : les overrides ne sont qu'en mémoire, donc le jeu revient à la normale.
  Ça va dans le bon sens.
- **Noms des actions** : à confirmer au démarrage en listant les actions du jeu
  (première étape du développement).
- **Manette** : version 1 = clavier seulement.

## 6 bis. Persistance par sauvegarde (étape 5)

| Événement | Effet |
|---|---|
| Arrivée dans une partie (`ConnectClientToPlayerObject`) | Les touches sauvegardées sont rechargées, puis un résumé s'affiche (pages de 4 lignes) |
| Sauvegarde de fin de journée (`StartOfRound.AutoSaveShipData`) | L'état actuel est enregistré. Quitter en pleine journée oublie les morts de la journée, comme pour le reste de la partie. |
| Équipage renvoyé (`playersFiredGameOver`) | Les touches reviennent à la normale et les données du mod sont effacées |
| Sortie du lobby (`GameNetworkManager.Disconnect`) | Les touches reviennent à la normale, mais les données sont conservées |

- **Hôte** : clé ES3 `LethalKeys_Bindings` dans le fichier de sauvegarde du jeu (`LCSaveFile1`…).
- **Client** : `BepInEx/config/LethalKeys/host_<SteamID de l'hôte>.txt`.
  Limite : si l'hôte supprime sa partie sans être renvoyé, l'ancien état reste chez le client.
- Format : `Action|index|chemin`, c'est-à-dire l'état final de chaque binding, réappliqué tel quel au chargement.

**Protection des réglages** : `CompleteRebind`, le seul endroit où le jeu sérialise les overrides,
est encadré par `Suspend`/`Resume`. Si le joueur réassigne lui-même une touche modifiée par le mod,
son choix l'emporte. Après `DiscardChangedSettings`, `ResetAllKeybinds` et `LoadSettingsFromPrefs`,
le mod réapplique ses changements.

## 7. Étapes de développement

1. Créer le projet (csproj + références au jeu) et un plugin vide qui se charge.
2. Lister les actions et bindings du jeu dans le log BepInEx.
3. Réassigner une touche en dur sur une commande de debug.
4. Brancher sur la mort, avec un tirage aléatoire et le pool anti-conflit.
5. Réinitialisation en fin de partie + protection de la sauvegarde.
6. Message à l'écran, configuration, puis paquet Thunderstore (manifest, icône, README).
