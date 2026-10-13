# Stack Island

*Catch it. Cook it. Stack it high.*

A compact carry-and-stack island tycoon for Windows and Linux, in the style of mobile hits like Idle Mafia and Supermarket
Tycoon: one bright walled yard at a time, a close camera, tall wobbling stacks, buy spots painted on the floor and
customers queueing with speech bubbles.

## How it plays

1. Walk into the **crab pen**. You swing at nearby crabs automatically; they drop crab meat. Walk over it to stack it.
2. Drop the meat on the **grill's left tray**. Crab cakes pile up on the **right tray**.
3. Stand on the painted **SELL** sign: cakes hop onto the counter. Customers on the other side take what their bubble
   asks for and pay in cash bundles at the end of the counter.
4. Walk over the cash to stack it on your back, then stand still on a **dark buy tile** to spend it:
   more crabs, a bigger stack, hire a Crab Catcher or a Waiter, a hotter or second grill, a better recipe.

## Controls

| Key | Action |
|---|---|
| WASD / arrows | Walk |
| Right-click | Walk to that spot |
| Mouse wheel | Zoom |
| Esc | Pause menu (volume, windowed, main menu, quit) |

## Working on it

- Unity **6000.3.25f1** (URP). Menu **Stack Island** has the tools.
- Everything is built in code at runtime from `Game.cs` (progression and prices live in `Game.BuildPads`).
- Art: Kenney CC0 packs plus models drawn in code (`Art/ProcArt.cs`: meat, crab cakes, cash bundles, the dome grill,
  counter, walls, cage fences, floor signs and buy tiles).
- `pwsh Tools/unity-batch.ps1 StackIsland.EditorTools.ProjectSetup.RunBatch` sets the project up (art library, scene).
- `pwsh Tools/run-tests.ps1` runs the EditMode and PlayMode tests.
- `pwsh Tools/build-and-shoot.ps1` builds `Builds/Windows/StackIsland.exe` and captures screenshots (the game plays
  itself on a throwaway save).

## Linux / Nix

On x86_64 Linux, with Nix flakes enabled:

```sh
cd personal/stackisland
nix run
```

The launcher prefers a native player in `Builds/Linux`. Until that player is built,
`nix run` uses the pinned upstream v0.1.0 Windows release with Wine. Nix supplies
Wine 11.16, DXVK, graphics/audio libraries and game files; no Unity license is needed to play
that release. This compatibility runner is separate from the native Linux port.
The standalone native player can also be launched with `Tools/run-native.sh` on
a Linux host with Unity's runtime libraries installed. Arguments after `--` reach
the player, for example:

```sh
nix run . -- -screen-fullscreen 0 -screen-width 1280 -screen-height 720
nix run .#wine                 # explicitly use the upstream Windows release
nix run .#native               # require the local native Linux build
```

### Build the native Linux player

Install **Unity 6000.3.25f1**, activate its license through Unity Hub, and install
**Linux Build Support (Mono)**. Then run:

```sh
export UNITY_EDITOR="$HOME/Unity/Hub/Editor/6000.3.25f1/Editor/Unity"
Tools/build-linux.sh
nix run .#native
# Optional archive for sharing:
Tools/build-linux.sh --zip
```

The build recreates the art library and main scene, targets `StandaloneLinux64`,
and includes Vulkan and OpenGL Core. Output is `Builds/Linux/StackIsland.x86_64`
and its accompanying data/runtime files. Keep that whole directory together.
The Unity menu also exposes **Stack Island → Build Linux** and **Build Linux + Zip**.
On systems where the downloaded editor needs an FHS runtime, launch the build
script through an appropriate Unity/Steam FHS environment. Nix does not activate
Unity or build the proprietary editor inside its sandbox.

Set `STACKISLAND_NATIVE_DIR` to an absolute player directory to run a native build
from another working directory. An explicitly set but invalid directory fails
rather than silently falling back. Native saves use Unity's Linux location:
`~/.config/unity3d/Stack Island/Stack Island/`.

Wine uses its own prefix at `${XDG_DATA_HOME:-$HOME/.local/share}/stackisland/wine`;
set `STACKISLAND_WINEPREFIX` to override it. Native and Wine saves are separate.
The runner uses the pinned development Wine package because Unity 6 mouse input
requires `EnableMouseInPointer`, implemented in Wine 11.3. Wine 11.0 can render
the game while leaving menu buttons and other mouse controls unresponsive.
The launcher supplies Mesa drivers for Intel/AMD on non-NixOS Linux. DXVK maps
the Windows build's Direct3D 11 renderer to Vulkan. Proprietary GPU users can set
`VK_DRIVER_FILES` and `LIBGL_DRIVERS_PATH` for their host. The native Nix launcher
syncs the build into `${XDG_CACHE_HOME:-$HOME/.cache}/stackisland/native/` and patches
that copy to use Nix's ELF loader; the original build remains untouched.
Unity build logs live in `Logs/build-linux.log`.

### Validation in this checkout

`nix build`, `nix flake check`, and ShellCheck pass. The Wine/DXVK runner completed
the game's automated playthrough through the Volcano Feast, captured 17 screenshots,
and exited successfully on Intel Raptor Lake graphics. Local captures are in
`Screenshots/linux-smoke/`; the player log is `Logs/linux-wine-player.log`.
The native launcher was checked with a compiled ELF probe (argument forwarding,
default native selection, and preservation of the original binary). The native
Unity player and Unity test suites have **not** been built/run here because no
licensed Unity editor is installed.
