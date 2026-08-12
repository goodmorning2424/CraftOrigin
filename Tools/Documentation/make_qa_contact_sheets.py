from __future__ import annotations

import re
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Documentation" / "_qa_handoff" / "final_page_captures"
OUTPUT = ROOT / "Documentation" / "_qa_handoff" / "contact_sheets"


def font(size: int):
    for path in (
        Path("C:/Windows/Fonts/YuGothB.ttc"),
        Path("C:/Windows/Fonts/meiryob.ttc"),
        Path("C:/Windows/Fonts/arialbd.ttf"),
    ):
        if path.exists():
            return ImageFont.truetype(str(path), size=size, index=0)
    return ImageFont.load_default()


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    groups: dict[str, list[tuple[int, Path]]] = defaultdict(list)
    pattern = re.compile(r"^(qa_\d+_[^_]+(?:_[^_]+)*)_page-(\d+)\.png$")
    for path in SOURCE.glob("*.png"):
        match = pattern.match(path.name)
        if match:
            groups[match.group(1)].append((int(match.group(2)), path))

    title_font = font(22)
    label_font = font(17)
    results = []
    for stem in sorted(groups):
        pages = sorted(groups[stem])
        for index in range(0, len(pages), 4):
            batch = pages[index : index + 4]
            sheet = Image.new("RGB", (1480, 1080), "white")
            draw = ImageDraw.Draw(sheet)
            draw.text((22, 12), f"{stem} / pages {batch[0][0]}–{batch[-1][0]}", font=title_font, fill="#1F4D78")
            for slot, (page_num, path) in enumerate(batch):
                source = Image.open(path).convert("RGB")
                # Capture the full Word page area below the ribbon.
                crop = source.crop((560, 276, source.width, source.height))
                crop.thumbnail((700, 470), Image.Resampling.LANCZOS)
                col = slot % 2
                row = slot // 2
                x = 22 + col * 725
                y = 56 + row * 505
                draw.rectangle((x - 2, y - 2, x + 704, y + 478), outline="#B7C9DF", width=2)
                draw.text((x + 4, y + 4), f"page {page_num}", font=label_font, fill="#2E74B5")
                sheet.paste(crop, (x + 2, y + 30))
            out = OUTPUT / f"{stem}_sheet-{index // 4 + 1:02d}.png"
            sheet.save(out)
            results.append(out)
    for path in results:
        print(path)


if __name__ == "__main__":
    main()
