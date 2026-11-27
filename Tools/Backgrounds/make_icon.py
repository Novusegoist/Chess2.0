"""Рисует временную иконку игры: горящий король на плашке в стиле кнопок.

Запуск из корня проекта:  python Tools/Backgrounds/make_icon.py
Результат в Assets/Art/Icon (Unity ставит их сам — Assets/Editor/ProjectSetup.cs):
- icon.png — основная иконка 1024x1024 со скруглёнными углами (ПК, Steam, Android);
- icon_opaque.png — та же без прозрачности (App Store не принимает иконки с прозрачными углами);
- android_bg.png, android_fg.png — слои адаптивной иконки Android 432x432 (фон и картинка;
  система сама обрезает её кругом, квадратом и т.п., поэтому главное — в центральных 66%).
"""
import os

from PIL import Image, ImageDraw, ImageFilter

S = 1024
SS = 2
FRAME = (31, 79, 99)
FILL = (133, 160, 210)
PIXEL = (214, 214, 236)
OUT = os.path.join('Assets', 'Art', 'Icon', 'icon.png')


def plate(size):
    """Плашка как у кнопок: тёмная рамка с вырезанными углами, внутри — градиент."""
    w = size
    r = int(w * 0.16)
    border = int(w * 0.055)
    img = Image.new('RGBA', (w, w), (0, 0, 0, 0))

    def shape(inset, color):
        layer = Image.new('L', (w, w), 0)
        d = ImageDraw.Draw(layer)
        d.rounded_rectangle([inset, inset, w - inset, w - inset], radius=r - inset // 2, fill=255)
        # вогнутые углы, как у плашек кнопок
        cut = r * 0.55 - inset * 0.3
        for cx, cy in [(inset, inset), (w - inset, inset), (inset, w - inset), (w - inset, w - inset)]:
            d.ellipse([cx - cut, cy - cut, cx + cut, cy + cut], fill=0)
        return layer

    outer = shape(int(w * 0.02), FRAME)
    inner = shape(int(w * 0.02) + border, FILL)

    frame = Image.new('RGBA', (w, w), FRAME + (255,))
    img.paste(frame, (0, 0), outer)

    # Внутри: от голубого сверху к тёмной бирюзе снизу.
    grad = Image.new('RGBA', (w, w))
    gd = ImageDraw.Draw(grad)
    top, bottom = (110, 140, 196), (36, 70, 96)
    for y in range(w):
        t = y / (w - 1)
        gd.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    img.paste(grad, (0, 0), inner)

    # Пиксели-клетки в верхних углах.
    px = Image.new('RGBA', (w, w), (0, 0, 0, 0))
    pd = ImageDraw.Draw(px)
    c = w // 14
    x0 = int(w * 0.02) + border + c // 2
    y0 = x0
    pattern = [(0, 0), (1, 0), (0, 1), (2, 1), (1, 2), (0, 3)]
    for gx, gy in pattern:
        for mirror in (False, True):
            x = w - x0 - (gx + 1) * c if mirror else x0 + gx * c
            pd.rectangle([x, y0 + gy * c, x + c, y0 + (gy + 1) * c], fill=PIXEL + (120 - gy * 22,))
    px.putalpha(Image.composite(px.getchannel('A'), Image.new('L', (w, w), 0), inner))
    img.alpha_composite(px)
    return img, inner


def main():
    w = S * SS
    img, inner = plate(w)

    draw_emblem(img, w, inner, 1.0)

    out = img.resize((S, S), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT, optimize=True)

    # Без прозрачности: углы заливаем цветом рамки.
    opaque = Image.new('RGBA', (S, S), FRAME + (255,))
    opaque.alpha_composite(out)
    opaque.convert('RGB').save(os.path.join(os.path.dirname(OUT), 'icon_opaque.png'), optimize=True)

    make_adaptive()
    print('ok')


def make_adaptive():
    """Слои адаптивной иконки Android: фон с клетками и прозрачный передний слой с королём."""
    a = 432
    w = a * SS
    bg = Image.new('RGBA', (w, w))
    d = ImageDraw.Draw(bg)
    top, bottom = (110, 140, 196), (36, 70, 96)
    for y in range(w):
        t = y / (w - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    px = Image.new('RGBA', (w, w), (0, 0, 0, 0))
    pd = ImageDraw.Draw(px)
    c = w // 12
    for gx, gy in [(0, 0), (1, 0), (0, 1), (2, 1), (1, 2), (0, 3)]:
        for mirror in (False, True):
            x = w - (gx + 1) * c - c if mirror else c + gx * c
            pd.rectangle([x, c + gy * c, x + c, c + (gy + 1) * c], fill=PIXEL + (110 - gy * 20,))
    bg.alpha_composite(px)
    bg.resize((a, a), Image.LANCZOS).convert('RGB').save(
        os.path.join(os.path.dirname(OUT), 'android_bg.png'), optimize=True)

    fg = Image.new('RGBA', (w, w), (0, 0, 0, 0))
    full = Image.new('L', (w, w), 255)
    draw_emblem(fg, w, full, 0.78)  # король целиком в центральных ~66%, огонь может выходить за край
    fg.resize((a, a), Image.LANCZOS).save(os.path.join(os.path.dirname(OUT), 'android_fg.png'), optimize=True)


def draw_emblem(img, w, inner, scale):
    """Огонь и король с тёмным контуром, по центру; scale — доля размера холста."""
    off = w * (1 - scale) / 2

    # Огонь за королём: несколько кадров анимации, размытое свечение.
    fire = Image.new('RGBA', (w, w), (0, 0, 0, 0))
    for name, fscale, dx in [('fire_20.png', 0.95, 0), ('fire_05.png', 0.75, -0.17), ('fire_40.png', 0.75, 0.17)]:
        f = Image.open(os.path.join('Assets', 'Resources', 'Fx', 'Fire', name)).convert('RGBA')
        h = int(w * scale * fscale)
        f = f.resize((int(f.width * h / f.height), h), Image.LANCZOS)
        fire.alpha_composite(f, (int(w / 2 - f.width / 2 + dx * w * scale), int(off + w * scale * 0.9 - h)))
    glow = fire.filter(ImageFilter.GaussianBlur(w * 0.04 * scale))
    img.alpha_composite(Image.composite(glow, Image.new('RGBA', (w, w), (0, 0, 0, 0)), inner))
    img.alpha_composite(Image.composite(fire, Image.new('RGBA', (w, w), (0, 0, 0, 0)), inner))

    # Король с тёмным контуром, чтобы читался и в 32x32.
    king = Image.open(os.path.join('Assets', 'Resources', 'Pieces', 'w_king.png')).convert('RGBA')
    king = king.crop(king.getchannel('A').getbbox())
    kh = int(w * 0.72 * scale)
    king = king.resize((int(king.width * kh / king.height), kh), Image.LANCZOS)
    kx, ky = (w - king.width) // 2, int(off + w * scale * 0.87) - kh

    pad = int(40 * SS * scale) + 4
    a = Image.new('L', (king.width + pad * 2, king.height + pad * 2), 0)
    a.paste(king.getchannel('A'), (pad, pad))
    outline = a.filter(ImageFilter.MaxFilter(2 * max(1, int(9 * SS * scale)) + 1))
    shadow = Image.new('RGBA', a.size, (18, 40, 54, 0))
    shadow.putalpha(outline)
    img.alpha_composite(shadow, (kx - pad, ky - pad))
    img.alpha_composite(king, (kx, ky))


if __name__ == '__main__':
    main()
