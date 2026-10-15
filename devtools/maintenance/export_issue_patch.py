"""Export reviewed shared gameplay changes from a saved desktop baseline."""
from pathlib import Path
import difflib

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / "_takeover_audit/issues_20261011/baseline"
FILES = ["Ocgcore/Ocgcore.cs", "Room/AIRoom.cs", "Room/DuelUndo.cs", "MonoHelpers/TcpHelper.cs", "SelectDeck/selectDeck.cs",
         "Ocgcore/OCGobjects/gameField.cs", "MonoHelpers/UIHelper.cs", "Room/DuelUndoPreview.cs"]
patch = []
for name in FILES:
    relative = "Assets/SibylSystem/" + name
    old = (BASE / Path(name).name).read_text(encoding="utf-8-sig")
    new = (ROOT / "YGOProUnity_V2" / relative).read_text(encoding="utf-8-sig")
    # Pinned layer: DuelUndo has never been rewritten by a CRLF patch;
    # the other three inputs are already rewritten by earlier Windows patches.
    ending = "\r\n" if name in ("Ocgcore/Ocgcore.cs", "Room/AIRoom.cs", "MonoHelpers/TcpHelper.cs") else "\n"
    patch.extend(line.rstrip("\r\n") + ending for line in difflib.unified_diff(
        old.splitlines(True), new.splitlines(True), fromfile="a/" + relative, tofile="b/" + relative))
target = ROOT / "YGOProAndroid/patches/0021-gameplay-reliability.patch"
target.write_bytes("".join(patch).encode("utf-8"))
print(target)
