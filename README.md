# LethalKeys

Mod Lethal Company : **à chaque mort, une de tes touches est réassignée au hasard.**
Description pour les joueurs : [Thunderstore/README.md](Thunderstore/README.md). Conception : [CONCEPTION.md](CONCEPTION.md).

## Compiler

```bash
dotnet build -c Debug     # copie le DLL dans le profil "Default" du Thunderstore Mod Manager (touches F8/F9/F10)
dotnet build -c Release   # crée dist/LethalKeys-<version>.zip, prêt pour Thunderstore
```

- Si le jeu est installé (`GameDir` dans le `.csproj`), le build utilise ses DLL. Sinon, il passe par les paquets NuGet
  `LethalCompany.GameLibs.Steam`, `BepInEx.Core` et `UnityEngine.Modules`, pour forcer ce mode : `-p:UseLocalGameLibs=false`.
- LethalConfig (dépendance optionnelle) : DLL du profil ou `-p:LethalConfigDll=<chemin>`.
- La version vient de `<Version>` du `.csproj`, ou de `-p:Version=X.Y.Z`.

## Publier une version

```bash
git tag v0.2.0
git push origin v0.2.0
```

Le workflow [Build](.github/workflows/build.yml) :
1. vérifie qu'il existe une section `## 0.2.0` dans `Thunderstore/CHANGELOG.md` ;
2. compile en version `0.2.0` et crée une **release GitHub** avec le `.zip` ;
3. **publie sur Thunderstore** (`UnamedGames-LethalKeys`) avec `tcli`, si le secret `TCLI_AUTH_TOKEN` existe.
   Les catégories et la communauté sont dans [Thunderstore/thunderstore.toml](Thunderstore/thunderstore.toml).

Chaque push sur `main` produit aussi le `.zip` en artefact du run.

Une version publiée sur Thunderstore est **définitive** : pour corriger, il faut un nouveau numéro.
