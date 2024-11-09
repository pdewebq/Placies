{
  lib,
  buildDotnetModule,
  dotnetCorePackages,
  nugetPackagesLockToNugetDeps,
  repoSrc
}:
let
  dotnet-sdk = dotnetCorePackages.sdk_8_0;
  dotnet-runtime = dotnetCorePackages.aspnetcore_8_0;
in
buildDotnetModule (finalAttrs: {
  pname = "Placies.Cli";
  version = lib.strings.fileContents "${repoSrc}/release-version.txt";
  inherit dotnet-sdk;
  inherit dotnet-runtime;
  src = repoSrc;
  projectFile = "src/Placies.Cli/Placies.Cli.fsproj";
  nugetDeps = nugetPackagesLockToNugetDeps {
    packagesLockJson = "${repoSrc}/src/Placies.Cli/packages.lock.json";
  };
})
