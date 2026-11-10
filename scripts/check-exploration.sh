#!/bin/zsh
set -eu
cd "${0:A:h}/.."
mkdir -p artifacts/exploration
./scripts/test-core.sh > artifacts/exploration-core-tests.log
dotnet build tests/Momo.Unity.Compile.csproj -c Release --no-restore > artifacts/exploration-csharp-desktop.log
dotnet build tests/Momo.Unity.Compile.csproj -c Release --no-restore -p:DefineConstants=UNITY_IOS > artifacts/exploration-csharp-compile.log
sdk_path="$(xcrun --sdk iphoneos --show-sdk-path)"
xcrun --sdk iphoneos clang++ -fobjc-arc -x objective-c++ -target arm64-apple-ios16.0 -isysroot "$sdk_path" -dynamiclib Assets/Plugins/iOS/MomoPosition.mm -framework Foundation -framework UIKit -framework ARKit -framework AVFoundation -o artifacts/exploration/MomoPosition-check.dylib
sim_sdk_path="$(xcrun --sdk iphonesimulator --show-sdk-path)"
xcrun --sdk iphonesimulator clang++ -fsyntax-only -fobjc-arc -x objective-c++ -target arm64-apple-ios16.0-simulator -isysroot "$sim_sdk_path" Assets/Plugins/iOS/MomoPosition.mm
python3 - <<'PY'
import pathlib, wave, subprocess
paths=list(pathlib.Path('Assets/Momo/Resources/CatAudio').glob('*.wav'))
assert len(paths)==8
for path in paths:
    with wave.open(str(path)) as f:
        assert f.getnchannels()==1 and f.getframerate()==22050
        assert .4<f.getnframes()/f.getframerate()<3
        assert f.getnframes()>0
for name in ['Mikan','Earl','Chai','Oreo','Yuki']:
    path=pathlib.Path('Assets/Momo/Resources/CatAudio')/('Meow'+name+'.mp3')
    assert path.is_file(), path
    subprocess.run(['ffmpeg','-v','error','-i',str(path),'-f','null','-'],check=True)
print('8 original WAV clips and 5 per-cat MP3 meows validated. Core, C# desktop/iOS branches and native ARKit compile/link passed.')
PY
