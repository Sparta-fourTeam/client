"""Copy approved/generated files unchanged into this workspace."""
from pathlib import Path
import json, shutil
ROOT=Path(__file__).resolve().parents[2]
records=json.loads((ROOT/'docs/asset-previews/existing-refresh-manifest.json').read_text(encoding='utf-8'))
for record in records:
    destination=(ROOT/record['path']).resolve()
    assert destination.is_relative_to((ROOT/'Assets/_Project/Sprites/Generated').resolve())
    destination.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(record['generatedSource'],destination)
print(f'Copied {len(records)} image files without changing pixels')
