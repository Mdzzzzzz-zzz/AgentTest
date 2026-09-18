from PIL import Image
import os

src = r"C:\Users\youku\.workbuddy\clipboard-images\clipboard-2026-09-16T03-04-31-743Z-ef6bbcc4.png"
out = r"D:\NewOne\Temp\_zoom"
os.makedirs(out, exist_ok=True)

im = Image.open(src).convert("RGB")
W, H = im.size
print("image size:", W, H)

regions = {
    "left_prop": (0, int(H * 0.18), int(W * 0.18), int(H * 0.60)),
    "aim_area": (int(W * 0.33), int(H * 0.58), int(W * 0.62), int(H * 0.96)),
    "enemies": (int(W * 0.52), int(H * 0.60), int(W * 0.70), int(H * 0.94)),
}

for name, box in regions.items():
    c = im.crop(box)
    scale = max(1, int(1000 / max(1, c.width)))
    c = c.resize((c.width * scale, c.height * scale), Image.NEAREST)
    p = os.path.join(out, name + ".png")
    c.save(p)
    print(p, c.size)

# 全图统计：洋红像素占比（缺材质会大面积洋红 255,0,255）
px = im.load()
magenta = 0
total = W * H
for y in range(0, H, 2):
    for x in range(0, W, 2):
        r, g, b = px[x, y]
        if r > 200 and g < 60 and b > 200:
            magenta += 1
sampled = (H // 2) * (W // 2)
print("magenta_ratio: %.4f%%  (sampled %d px)" % (100.0 * magenta / sampled, sampled))
