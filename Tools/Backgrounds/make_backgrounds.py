"""Рисует фоны меню и партии в стиле кнопок (тёмная бирюза, голубой, сиреневые пиксели-клетки).

Запуск из корня проекта:  python Tools/Backgrounds/make_backgrounds.py
Нужен только Pillow. Результат: Assets/Resources/UI/Backgrounds/bg_menu.png и bg_game.png.
"""
import os
import random

from PIL import Image, ImageDraw, ImageFilter

W, H = 1920, 1080
SS = 2  # рисуем в 2 раза крупнее и уменьшаем — сглаженные края

TOP = (30, 66, 84)        # как рамка кнопок, чуть светлее
BOTTOM = (14, 30, 40)
FRAME = (31, 79, 99)
FILL = (133, 160, 210)    # заливка кнопок
PIXEL = (214, 214, 236)   # сиреневые клетки на кнопках

OUT = os.path.join('Assets', 'Resources', 'UI', 'Backgrounds')
PIECES = os.path.join('Assets', 'Resources', 'Pieces')


def gradient(w, h, top, bottom):
    img = Image.new('RGB', (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img.convert('RGBA')


def glow(w, h, cx, cy, radius, color, strength):
    """Мягкое пятно света."""
    layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.ellipse([cx - radius, cy - radius * 0.6, cx + radius, cy + radius * 0.6], fill=color + (strength,))
    return layer.filter(ImageFilter.GaussianBlur(radius * 0.45))


def vignette(w, h, strength):
    mask = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([-w * 0.15, -h * 0.25, w * 1.15, h * 1.25], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(w * 0.08))
    dark = Image.new('RGBA', (w, h), (6, 14, 20, strength))
    dark.putalpha(Image.eval(mask, lambda v: int((255 - v) * strength / 255)))
    return dark


def pixel_cluster(d, x0, y0, cell, rng, density, flip=False):
    """Россыпь квадратиков, как в углах кнопок: плотнее у края, реже к центру."""
    for gy in range(7):
        for gx in range(7):
            p = density * (1 - gx / 7) * (1 - gy / 9)
            if rng.random() > p:
                continue
            size = cell * rng.choice([1, 1, 1, 0.5])
            x = x0 - gx * cell - size if flip else x0 + gx * cell
            y = y0 + gy * cell
            a = int(rng.uniform(18, 55) * (1 - gx / 9))
            col = PIXEL if rng.random() < 0.7 else FILL
            d.rectangle([x, y, x + size, y + size], fill=col + (a,))


def floor(w, h, horizon, fade_to):
    """Шахматный пол в перспективе, растворяется к горизонту."""
    layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cam, f = w / 2, 1.0, h * 0.9
    tiles_x = 14

    def proj(x, z):
        return cx + x * f / z, horizon + cam * f / z

    for zi in range(26):
        z0, z1 = 0.9 + zi * 0.5, 0.9 + (zi + 1) * 0.5
        depth = zi / 26
        for xi in range(-tiles_x, tiles_x):
            light = (xi + zi) % 2 == 0
            x0, x1 = xi * 0.5, (xi + 1) * 0.5
            quad = [proj(x0, z0), proj(x1, z0), proj(x1, z1), proj(x0, z1)]
            alpha = int((1 - depth) ** 1.6 * (70 if light else 40))
            col = FILL if light else FRAME
            d.polygon(quad, fill=col + (alpha,))
    # горизонт: плавный переход в фон
    fade = Image.new('L', (w, h), 255)
    fd = ImageDraw.Draw(fade)
    for y in range(int(horizon), int(fade_to)):
        t = (y - horizon) / (fade_to - horizon)
        fd.line([(0, y), (w, y)], fill=int(255 * t))
    fd.rectangle([0, 0, w, horizon], fill=0)
    layer.putalpha(Image.composite(layer.getchannel('A'), Image.new('L', (w, h), 0), fade))
    return layer


def silhouette(name, height, color, alpha):
    src = Image.open(os.path.join(PIECES, name + '.png')).convert('RGBA')
    src = src.crop(src.getchannel('A').getbbox())
    k = height / src.height
    src = src.resize((int(src.width * k), int(height)), Image.LANCZOS)
    out = Image.new('RGBA', src.size, color + (0,))
    out.putalpha(src.getchannel('A').point(lambda v: v * alpha // 255))
    return out


RIM_PAD = 60 * SS


def rim(sil, color, alpha, blur):
    """Подсветка контура силуэта (с запасом по краям, чтобы размытие не обрезалось)."""
    a = Image.new('L', (sil.width + RIM_PAD * 2, sil.height + RIM_PAD * 2), 0)
    a.paste(sil.getchannel('A'), (RIM_PAD, RIM_PAD))
    a = a.filter(ImageFilter.GaussianBlur(blur))
    g = Image.new('RGBA', a.size, color + (0,))
    g.putalpha(a.point(lambda v: min(255, v * alpha // 60)))
    return g


def paste_on_floor(img, sil, cx, bottom):
    x, y = int(cx - sil.width / 2), int(bottom - sil.height)
    img.alpha_composite(rim(sil, FILL, 40, 14 * SS), (x - RIM_PAD, y - RIM_PAD))
    img.alpha_composite(sil, (x, y))
    # тень на полу
    shadow = Image.new('RGBA', (sil.width * 2, sil.width // 2), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([sil.width * 0.4, 0, sil.width * 1.6, sil.width // 3], fill=(4, 10, 14, 120))
    shadow = shadow.filter(ImageFilter.GaussianBlur(12 * SS))
    img.alpha_composite(shadow, (int(cx - sil.width), int(bottom - sil.width // 8)))


def make_menu():
    w, h = W * SS, H * SS
    rng = random.Random(7)
    img = gradient(w, h, TOP, BOTTOM)
    horizon = h * 0.58
    img.alpha_composite(glow(w, h, w / 2, horizon, w * 0.42, FILL, 70))
    img.alpha_composite(floor(w, h, horizon, h * 0.78))

    # Силуэты фигур по краям, центр свободен под кнопки.
    dark = (12, 28, 38)
    paste_on_floor(img, silhouette('w_king', h * 0.62, dark, 235), w * 0.13, h * 0.95)
    paste_on_floor(img, silhouette('w_knight', h * 0.46, dark, 235), w * 0.86, h * 0.93)
    paste_on_floor(img, silhouette('w_pawn', h * 0.24, dark, 225), w * 0.24, h * 0.86)
    paste_on_floor(img, silhouette('w_rook', h * 0.30, dark, 225), w * 0.76, h * 0.83)

    layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    px = ImageDraw.Draw(layer)
    cell = 34 * SS
    pixel_cluster(px, 40 * SS, 40 * SS, cell, rng, 0.9)
    pixel_cluster(px, w - 40 * SS, 40 * SS, cell, rng, 0.9, flip=True)
    img.alpha_composite(layer)
    img.alpha_composite(vignette(w, h, 150))
    return img.resize((W, H), Image.LANCZOS).convert('RGB')


def make_game():
    w, h = W * SS, H * SS
    rng = random.Random(11)
    img = gradient(w, h, (26, 58, 74), (12, 26, 35))
    img.alpha_composite(glow(w, h, w / 2, h / 2, h * 0.75, FILL, 38))

    # Крупная редкая шахматная сетка — едва заметная.
    grid = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grid)
    cell = 120 * SS
    for gy in range(h // cell + 1):
        for gx in range(w // cell + 2):
            if (gx + gy) % 2 == 0:
                gd.rectangle([gx * cell - cell // 2, gy * cell, gx * cell + cell // 2, gy * cell + cell],
                             fill=FRAME + (34,))
    img.alpha_composite(grid)

    top = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    px = ImageDraw.Draw(top)
    c = 30 * SS
    pixel_cluster(px, 30 * SS, 30 * SS, c, rng, 0.8)
    pixel_cluster(px, w - 30 * SS, 30 * SS, c, rng, 0.8, flip=True)
    img.alpha_composite(top)
    # нижние углы — зеркально вверх
    low = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(low)
    pixel_cluster(ld, 30 * SS, 30 * SS, c, rng, 0.7)
    pixel_cluster(ld, w - 30 * SS, 30 * SS, c, rng, 0.7, flip=True)
    img.alpha_composite(low.transpose(Image.FLIP_TOP_BOTTOM))
    img.alpha_composite(vignette(w, h, 170))
    return img.resize((W, H), Image.LANCZOS).convert('RGB')


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    make_menu().save(os.path.join(OUT, 'bg_menu.png'), optimize=True)
    make_game().save(os.path.join(OUT, 'bg_game.png'), optimize=True)
    print('ok')
