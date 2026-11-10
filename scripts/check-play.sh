#!/bin/zsh
set -eu
cd "${0:A:h}/.."
mkdir -p artifacts
./scripts/test-core.sh > artifacts/wand-core-tests.log 2>&1
DOTNET_CLI_HOME="$PWD/.tools/dotnet-home" dotnet build tests/Momo.Unity.Compile.csproj -c Release --no-restore -p:DefineConstants=UNITY_IOS > artifacts/wand-ios-csharp.log 2>&1
./scripts/build.sh mac
review_player="$PWD/Builds/macOS/Momo.app/Contents/MacOS/Momo"
for size in 320x700 390x844 768x1024; do
  "$review_player" -batchmode --wand-challenge-review "--play-review-size=$size" -logFile "$PWD/artifacts/wand-player-$size.log" > "$PWD/artifacts/wand-player-$size-stdout.log" 2>&1
  test -f "artifacts/wand-challenge/$size/PASS.txt"
  test ! -f "artifacts/wand-challenge/$size/FAIL.txt"
  cat "artifacts/wand-challenge/$size/PASS.txt"
done
for mode in quiet-hud multi-cat garden-feeding bath; do
  "$review_player" -batchmode "--$mode-review" -logFile "$PWD/artifacts/wand-regression-$mode.log" > "$PWD/artifacts/wand-regression-$mode-stdout.log" 2>&1
done
test -f artifacts/quiet-hud/PASS.txt
test -f artifacts/multicat/PASS.txt
test -f artifacts/garden-feeding/PASS.txt
test -f artifacts/bathing/PASS.txt
