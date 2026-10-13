{
  description = "Stack Island for Linux: native Unity player and Wine release runner";
  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs =
    { nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
      release = pkgs.stdenvNoCC.mkDerivation {
        pname = "stackisland-windows-data";
        version = "0.1.0";
        src = pkgs.fetchurl {
          url = "https://github.com/RuanDaMan/StackIsland/releases/download/v0.1.0/StackIsland-v0.1.0-Windows.zip";
          hash = "sha256-qsY8EvyD0vJayVgOIhqDUNDoVMNYoNf+2PdOfp4m6q0=";
        };
        nativeBuildInputs = [ pkgs.unzip ];
        dontUnpack = true;
        installPhase = ''
          mkdir -p $out/share/stackisland
          unzip -q $src -d $out/share/stackisland
          test -f $out/share/stackisland/StackIsland.exe
        '';
      };
      libraries = with pkgs; [
        glibc
        stdenv.cc.cc.lib
        dbus
        libunwind
        util-linux.lib
        fontconfig
        freetype
        libGL
        libglvnd
        mesa
        vulkan-loader
        libx11
        libxcursor
        libxrandr
        libxi
        libxinerama
        libxext
        libxrender
        libxfixes
        libxcb
        libxkbcommon
        libxcomposite
        libxxf86vm
        alsa-lib
        libpulseaudio
        udev
        glib
        zlib
        openssl
        icu
      ];
      graphics = ''
        export LIBGL_DRIVERS_PATH="''${LIBGL_DRIVERS_PATH:-/run/opengl-driver/lib/dri:${pkgs.mesa}/lib/dri}"
        export __EGL_VENDOR_LIBRARY_DIRS="''${__EGL_VENDOR_LIBRARY_DIRS:-/run/opengl-driver/share/glvnd/egl_vendor.d:${pkgs.mesa}/share/glvnd/egl_vendor.d}"
        export VK_ADD_DRIVER_FILES="''${VK_ADD_DRIVER_FILES:-${pkgs.mesa}/share/vulkan/icd.d/intel_icd.x86_64.json:${pkgs.mesa}/share/vulkan/icd.d/radeon_icd.x86_64.json:${pkgs.mesa}/share/vulkan/icd.d/lvp_icd.x86_64.json}"
      '';
      native = pkgs.writeShellApplication {
        name = "stackisland-native";
        runtimeInputs = [
          pkgs.coreutils
          pkgs.rsync
          pkgs.patchelf
        ];
        text = graphics + ''
          source_dir="''${STACKISLAND_NATIVE_DIR:-$PWD/Builds/Linux}"
          if [[ ! -f "$source_dir/StackIsland.x86_64" || ! -d "$source_dir/StackIsland_Data" ]]; then
            echo "Native build missing at $source_dir. Run Tools/build-linux.sh first, or set STACKISLAND_NATIVE_DIR." >&2
            exit 1
          fi
          source_dir=$(realpath "$source_dir")
          # Patch a writable copy, preserving the original Unity build for distribution.
          build_id=$(printf '%s' "$source_dir" | sha256sum | cut -d ' ' -f 1)
          game_dir="''${XDG_CACHE_HOME:-$HOME/.cache}/stackisland/native/$build_id"
          mkdir -p "$game_dir"
          rsync -rlt --checksum --delete --chmod=u+w "$source_dir/" "$game_dir/"
          chmod +x "$game_dir/StackIsland.x86_64"
          patchelf --set-interpreter ${pkgs.stdenv.cc.bintools.dynamicLinker} "$game_dir/StackIsland.x86_64"
          export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath libraries}:$game_dir:$game_dir/MonoBleedingEdge/x86_64''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
          cd "$game_dir"
          exec ./StackIsland.x86_64 "$@"
        '';
      };
      wine = pkgs.writeShellApplication {
        name = "stackisland-wine";
        runtimeInputs = [
          pkgs.coreutils
          # Unity 6 uses EnableMouseInPointer, implemented in Wine 11.3+.
          pkgs.wineWow64Packages.unstable
        ];
        text = graphics + ''
          export WINEPREFIX="''${STACKISLAND_WINEPREFIX:-''${XDG_DATA_HOME:-$HOME/.local/share}/stackisland/wine}"
          export WINEDEBUG="''${WINEDEBUG:--all}"
          export WINEDLLOVERRIDES="winemenubuilder.exe=d;d3d11,dxgi=n''${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}"
          mkdir -p "$WINEPREFIX"
          if [[ ! -f "$WINEPREFIX/system.reg" ]]; then
            # Unity ships Mono; suppress Wine's unrelated Mono/Gecko installers.
            WINEDLLOVERRIDES="mscoree,mshtml=d;$WINEDLLOVERRIDES" wineboot --init
            wineserver --wait
          fi
          for dll in d3d11 dxgi; do
            ln -sfn "${pkgs.dxvk.bin}/x64/$dll.dll" "$WINEPREFIX/drive_c/windows/system32/$dll.dll"
          done
          export DXVK_STATE_CACHE_PATH="''${XDG_CACHE_HOME:-$HOME/.cache}/stackisland/dxvk"
          mkdir -p "$DXVK_STATE_CACHE_PATH"
          cd ${release}/share/stackisland
          exec wine ./StackIsland.exe -force-d3d11 "$@"
        '';
      };
      game = pkgs.writeShellApplication {
        name = "stackisland";
        text = ''
          if [[ -n "''${STACKISLAND_NATIVE_DIR:-}" || -f "$PWD/Builds/Linux/StackIsland.x86_64" ]]; then
            exec ${native}/bin/stackisland-native "$@"
          fi
          echo "Stack Island: running Windows release with Wine (native build absent)." >&2
          exec ${wine}/bin/stackisland-wine "$@"
        '';
      };
    in
    {
      packages.${system} = {
        default = game;
        inherit
          game
          native
          wine
          release
          ;
      };
      apps.${system} = {
        default = {
          type = "app";
          meta.description = "Run Stack Island, preferring a local native Linux build";
          program = "${game}/bin/stackisland";
        };
        native = {
          type = "app";
          meta.description = "Run a local native Linux build of Stack Island";
          program = "${native}/bin/stackisland-native";
        };
        wine = {
          type = "app";
          meta.description = "Run the pinned Stack Island Windows release with Wine and DXVK";
          program = "${wine}/bin/stackisland-wine";
        };
      };
      devShells.${system}.default = pkgs.mkShell {
        packages = [
          pkgs.bash
          pkgs.shellcheck
          pkgs.unzip
          pkgs.nixfmt
        ];
      };
      formatter.${system} = pkgs.nixfmt;
    };
}
