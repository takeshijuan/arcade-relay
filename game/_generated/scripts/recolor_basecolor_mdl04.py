"""Deterministic HSV cluster remap for MDL-04 Warbeast baseColorTexture.
Clusters (by hue/value on the raw Meshy baseColorTexture):
  - body:       hue 250-335 (magenta/purple), V>=0.45  -> recolor toward #973FA5
  - underbelly: hue 250-335 (magenta/purple), V<0.45   -> recolor toward #3A0104
                (raw texture has no true dark-maroon pixels; the AO-darkened
                 band of the body cluster is repurposed as the underbelly
                 shading region, matching the concept image's visible dark
                 belly patch and the same technique used for MDL-01/02 base color)
  - horn/eye:   hue <20 or >340 (red family)            -> recolor toward #C71A23
Value (brightness) is rescaled per-cluster to the target's V as the new
cluster mean while preserving each pixel's relative shading/AO variation.
Hue/Sat are set to the target color's hue/sat (matches MDL-01/02 method).
"""
import colorsys
import numpy as np
from PIL import Image

SRC = "/tmp/warbeast-basecolor.png"
OUT = "/tmp/mdl04work/warbeast-basecolor-corrected.png"

TARGETS = {
    "body": "973FA5",
    "underbelly": "3A0104",
    "horn": "C71A23",
}


def hex_to_hsv(hexcode):
    r = int(hexcode[0:2], 16) / 255
    g = int(hexcode[2:4], 16) / 255
    b = int(hexcode[4:6], 16) / 255
    return colorsys.rgb_to_hsv(r, g, b)


targets_hsv = {k: hex_to_hsv(v) for k, v in TARGETS.items()}

im = Image.open(SRC).convert("RGB")
arr = np.array(im).astype(float) / 255.0
hsv_im = np.array(im.convert("HSV")).astype(float)
H = hsv_im[..., 0] / 255 * 360
S = hsv_im[..., 1] / 255
V = hsv_im[..., 2] / 255

red_mask = (H < 20) | (H > 340)
magenta_mask = (H >= 250) & (H <= 335) & ~red_mask
body_mask = magenta_mask & (V >= 0.45)
underbelly_mask = magenta_mask & (V < 0.45)
horn_mask = red_mask

print("pixel counts: body=%d underbelly=%d horn=%d unclassified=%d" % (
    body_mask.sum(), underbelly_mask.sum(), horn_mask.sum(),
    H.size - body_mask.sum() - underbelly_mask.sum() - horn_mask.sum()))

out_hsv = hsv_im.copy()

for name, mask in [("body", body_mask), ("underbelly", underbelly_mask), ("horn", horn_mask)]:
    if mask.sum() == 0:
        continue
    th, ts, tv = targets_hsv[name]
    cluster_v_mean = V[mask].mean()
    scale = tv / cluster_v_mean if cluster_v_mean > 0 else 1.0
    new_v = np.clip(V[mask] * scale, 0.0, 1.0)
    out_hsv[..., 0][mask] = th * 255
    out_hsv[..., 1][mask] = ts * 255
    out_hsv[..., 2][mask] = new_v * 255

out_hsv = np.clip(out_hsv, 0, 255).astype(np.uint8)
out_im = Image.fromarray(out_hsv, mode="HSV").convert("RGB")
out_im.save(OUT)
print("saved", OUT)

# --- verification: recompute per-cluster avg RGB + distance to target ---
out_rgb = np.array(out_im)


def dist(a, b):
    return sum((a[i] - b[i]) ** 2 for i in range(3)) ** 0.5


for name, mask in [("body", body_mask), ("underbelly", underbelly_mask), ("horn", horn_mask)]:
    if mask.sum() == 0:
        print(name, "NO PIXELS")
        continue
    avg = tuple(int(round(x)) for x in out_rgb[mask].mean(axis=0))
    target_rgb = tuple(int(TARGETS[name][i:i + 2], 16) for i in (0, 2, 4))
    print(name, "avg=%s dist=%.1f pct_within_40=%.1f%%" % (
        avg, dist(avg, target_rgb),
        100.0 * (np.linalg.norm(out_rgb[mask].astype(float) - np.array(target_rgb), axis=1) < 40).mean()
    ))
