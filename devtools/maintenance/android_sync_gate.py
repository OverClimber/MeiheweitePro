"""Check shared feature parity and emit every remaining platform source delta."""
from pathlib import Path
import argparse
import difflib
import hashlib
import json

ROOT = Path(__file__).resolve().parents[3]
SHARED = [
    'SibylSystem/Servant.cs',
    'old/UiverseAssests/art_plugin/thunder/RAYOS/Sources/LightningBolt.cs',
    'SibylSystem/CardDescription/CardDescLock.cs',
    'SibylSystem/CardDescription/CardSearchWindow.cs',
    'SibylSystem/deckManager/DeckManager.cs',
    'SibylSystem/deckManager/DeckDragGeometry.cs',
    'SibylSystem/Setting/Setting.cs',
    'SibylSystem/Room/DuelUndo.cs',
    'SibylSystem/Room/DuelUndoPreview.cs',
    'SibylSystem/ResourceManagers/ManagedPackInstall.cs',
    'SibylSystem/ResourceManagers/PictureAbsentCache.cs',
    'SibylSystem/ResourceManagers/ZipImageIndex.cs',
    'SibylSystem/ResourceManagers/CardScriptUpdater.cs',
    'ArtSystem/deckManager/cardPicLoader.cs',
    'SibylSystem/Ocgcore/OCGobjects/gameCard.cs',
    'SibylSystem/Ocgcore/OCGobjects/gameField.cs',
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    out = parser.parse_args().out.resolve()
    desktop = ROOT / 'YGOProUnity_V2'
    stage = out / 'android-stage'
    result = {'status': 'failed', 'identical_shared_features': [], 'differences': [], 'failures': []}
    read = lambda p: p.read_text(encoding='utf-8-sig')
    for rel in SHARED:
        a, b = desktop / 'Assets' / rel, stage / 'Assets' / rel
        if not a.exists() or not b.exists() or read(a) != read(b): result['failures'].append(rel)
        else: result['identical_shared_features'].append(rel)
    # Native protocol differences remain platform-specific; compare this batch's
    # shared callback lifetime and ReloadField implementations explicitly.
    core_a = read(desktop/'Assets/SibylSystem/coreWrapper.cs')
    core_b = read(stage/'Assets/SibylSystem/coreWrapper.cs')
    for start, end in [
        ('        // Native code retains', '        [DllImport'),
        ('        public static void set_card_api', '        [DllImport'),
        ('        static void CopyReloadField', '        int move('),
        ('                case GameMessage.ReloadField:', '                case GameMessage.AiName:')]:
        def section(text):
            begin = text.index(start)
            return text[begin:text.index(end, begin + len(start))]
        if section(core_a) != section(core_b): result['failures'].append('coreWrapper shared section: '+start.strip())
    diffs = []
    for folder in ['Assets/SibylSystem', 'Assets/ArtSystem']:
        for a in sorted((desktop / folder).rglob('*.cs')):
            rel = a.relative_to(desktop); b = stage / rel
            if not b.exists(): result['failures'].append(str(rel) + ' missing'); continue
            if read(a) != read(b):
                delta = list(difflib.unified_diff(read(a).splitlines(True), read(b).splitlines(True),
                    fromfile='desktop/'+rel.as_posix(), tofile='android/'+rel.as_posix()))
                result['differences'].append({'path': str(rel), 'lines': len(delta)})
                diffs.extend(delta)
    (out / 'platform-differences.diff').write_text(''.join(diffs), encoding='utf-8')
    manifest = json.loads((out / 'android-result.json').read_text(encoding='utf-8'))
    for name, expected in manifest['patches'].items():
        if hashlib.sha256((ROOT/'YGOProAndroid/patches'/name).read_bytes()).hexdigest() != expected:
            result['failures'].append(name+' changed after compile')
    for name, expected in manifest['layer_sources'].items():
        if hashlib.sha256((ROOT/'YGOProAndroid'/name).read_bytes()).hexdigest() != expected:
            result['failures'].append(name+' changed after compile')
    sources = json.loads((out/'desktop/desktop-source-sha256.json').read_text(encoding='utf-8'))
    for name, expected in sources.items():
        if hashlib.sha256((desktop/name).read_bytes()).hexdigest() != expected:
            result['failures'].append(name+' changed after desktop compile')
    if manifest['status'] != 'passed': result['failures'].append('Android compile did not pass')
    if not result['failures']: result['status']='passed'
    (out/'sync-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Shared feature parity: '+result['status']+'; identical='+str(len(result['identical_shared_features']))
          +'; remaining platform differences='+str(len(result['differences'])))
    if result['failures']: print(result['failures'])
    return 0 if result['status']=='passed' else 1


if __name__ == '__main__':
    raise SystemExit(main())
