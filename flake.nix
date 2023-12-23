{
  description = "Placies";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
    nuget-packageslock2nix = {
      url = "github:mdarocha/nuget-packageslock2nix/main";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = inputs@{ self, flake-parts, nuget-packageslock2nix, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [ "x86_64-linux" ];
      perSystem = { system, pkgs, ... }:
        let
          dotnet-sdk = pkgs.dotnet-sdk_8;
          dotnet-aspnetcore-runtime = pkgs.dotnet-aspnetcore_8;
        in rec {
          packages.placies-allowlist-proxy-gateway = pkgs.buildDotnetModule {
            pname = "Placies.Gateways.AllowlistProxyGateway.Server";
            version = "0.0.1";
            src = self;
            projectFile = "src/Gateways/Placies.Gateways.AllowlistProxyGateway.Server/Placies.Gateways.AllowlistProxyGateway.Server.fsproj";
            inherit dotnet-sdk;
            dotnet-runtime = dotnet-aspnetcore-runtime;
            nugetDeps = nuget-packageslock2nix.lib {
              inherit system;
              lockfiles = [ ./src/Gateways/Placies.Gateways.AllowlistProxyGateway.Server/packages.lock.json ];
            };
          };
          packages.placies-allowlist-proxy-gateway-docker-image = pkgs.dockerTools.buildLayeredImage {
            name = "placies-allowlist-proxy-gateway-docker-image";
            contents = [
              pkgs.busybox
              pkgs.cacert
              packages.placies-allowlist-proxy-gateway
            ];
            config = {
              Env = [
                "SSL_CERT_FILE=${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt"
              ];
              Cmd = [ "/bin/Placies.Gateways.AllowlistProxyGateway.Server" ];
            };
          };
        };
    };
}
