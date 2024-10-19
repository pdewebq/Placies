{
  description = "Placies";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    nuget-packages-lock2nuget-deps.url = "github:Prunkles/nuget-packages-lock2nuget-deps";
  };

  outputs = inputs@{ nixpkgs, flake-parts, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [ "x86_64-linux" ];
      perSystem = { lib, pkgs, ... }:
        let
          nugetPackagesLockToNugetDeps = pkgs.callPackage inputs.nuget-packages-lock2nuget-deps.lib.nugetPackagesLockToNugetDeps { inherit nixpkgs; };
          # Remove .nix files from src for better build caches
          repoSrc = builtins.path {
            path = ./.;
            name = "placies-src";
            recursive = true;
            filter = path: type: !(type == "regular" && lib.hasSuffix ".nix" (builtins.baseNameOf path));
          };
        in
        {
          packages.placies-allowlist-proxy-gateway = pkgs.callPackage ./nix/pkgs/placies-allowlist-proxy-gateway {
            inherit nugetPackagesLockToNugetDeps;
            inherit repoSrc;
          };
        };
    };
}
