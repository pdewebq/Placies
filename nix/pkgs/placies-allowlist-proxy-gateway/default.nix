{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  nugetPackagesLockToNugetDeps,
  callPackage,
  repoSrc
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.aspnetcore_8_0;
in
buildDotnetModule (finalAttrs: {
  pname = "Placies.Gateways.AllowlistProxyGateway.Server";
  version = lib.strings.fileContents "${repoSrc}/release-version.txt";
  inherit dotnet-sdk;
  inherit dotnet-runtime;
  src = repoSrc;
  projectFile = "src/Gateways/Placies.Gateways.AllowlistProxyGateway.Server/Placies.Gateways.AllowlistProxyGateway.Server.fsproj";
  nugetDeps = nugetPackagesLockToNugetDeps {
    packagesLockJson = "${repoSrc}/src/Gateways/Placies.Gateways.AllowlistProxyGateway.Server/packages.lock.json";
  };

  passthru = {
    dockerImage = callPackage ./docker-image.nix { placies-allowlist-proxy-gateway = finalAttrs.finalPackage; };
  };
})
