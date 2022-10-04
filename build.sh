#!/usr/bin/env bash

# targets
targets=("win-x64" "osx-x64" "osx-arm64")

for target in "${targets[@]}"; do
  #build
  dotnet publish -c Release -r $target -p:PublishSingleFile=true -p:SelfContained=true

  # archive publish dir
  tar -czf $target.tar.gz -C Server/bin/Release/net6.0/$target/publish .
done
