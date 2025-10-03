{
  description = "A very basic flake";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";
  };

  outputs = { self, nixpkgs }: {
    apps.x86_64-linux.ci_publish =
      let
        pkgs = nixpkgs.legacyPackages.x86_64-linux;
        script = pkgs.writeShellApplication {
          name = "ci_publish";
          runtimeInputs = with pkgs; [
            dotnet-sdk
          ];
          text = ''
            dotnet build --configuration Release
            dotnet test --configuration Release
            dotnet pack --configuration Release --output ./packages \
              -p:RealPackageId=DarkLink.ParserGen
            # dotnet pack ./DarkLink.ParserGen/DarkLink.ParserGen.csproj \
            #   --configuration Release --output ./packages \
            #   -p:RealPackageId=DarkLink.ParserGen.Bootstrap
            dotnet nuget push ./packages/* --skip-duplicate --source "$NUGET_REPO" --api-key "$NUGET_APIKEY"
          '';
        };
      in
      {
        type = "app";
        program = pkgs.lib.getExe script;
      };
  };
}
