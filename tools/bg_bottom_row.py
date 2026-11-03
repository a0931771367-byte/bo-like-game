#!/usr/bin/env python3
"""Prints the average colour of the bottom row of each stage painting, for Background.BottomRowColour.

    python3 tools/bg_bottom_row.py

Compressed iOS textures can't be read at runtime, so the values are tabled in Background.cs; rerun this
whenever a painting under unity/Assets/Resources/Bg changes and copy the numbers over.
"""
import glob, os, sys
try:
    from PIL import Image
except ImportError:
    sys.exit("pip install pillow")

root = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Resources", "Bg")
for path in sorted(glob.glob(os.path.join(root, "*.png"))):
    im = Image.open(path).convert("RGB")
    row = [im.getpixel((x, im.height - 1)) for x in range(im.width)]
    r, g, b = (sum(c[i] for c in row) / len(row) / 255 for i in range(3))
    print(f'case "{os.path.splitext(os.path.basename(path))[0]}": return new Color({r:.3f}f, {g:.3f}f, {b:.3f}f);')
