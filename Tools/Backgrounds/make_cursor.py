"""Рисует курсор-треугольник («кристалл») в цветах кнопок (тёмная бирюза, голубой, сиреневый).

Запуск из корня проекта:  python Tools/Backgrounds/make_cursor.py
Результат: Assets/Resources/UI/cursor.png (32x32, кончик треугольника — левый верхний угол).
"""
import os

from PIL import Image, ImageDraw, ImageFilter

N = 32
K = 16  # рисуем в 16 раз крупнее и уменьшаем
FRAME = (31, 79, 99)
FILL_TOP = (226, 226, 244)
FILL_BOTTOM = (133, 160, 210)
OUT = os.path.join('Assets', 'Resources', 'UI', 'cursor.png')

# Треугольник в клетках 32x32: кончик в углу, остриём вверх-влево.
ARROW = [(1.6, 1.6), (21, 9.5), (9.5, 21)]
# Задняя сторона чуть вогнута — треугольник «наконечником». По линии кончик → выемка
# он делится на светлую и тёмную грань.
NOTCH = (13, 13)
SHAPE = [ARROW[0], ARROW[1], NOTCH, ARROW[2]]


def scaled(points, k, grow=0.0):
    cx = sum(p[0] for p in points) / len(points)
    cy = sum(p[1] for p in points) / len(points)
    out = []
    for x, y in points:
        dx, dy = x - cx, y - cy
        d = (dx * dx + dy * dy) ** 0.5 or 1
        out.append(((x + dx / d * grow) * k, (y + dy / d * grow) * k))
    return out


def main():
    w = N * K
    img = Image.new('RGBA', (w, w), (0, 0, 0, 0))

    # Мягкая тень.
    shadow = Image.new('L', (w, w), 0)
    ImageDraw.Draw(shadow).polygon([(x + 1.5 * K, y + 1.5 * K) for x, y in scaled(SHAPE, K)], fill=110)
    shadow = shadow.filter(ImageFilter.GaussianBlur(K * 0.9))
    img.alpha_composite(Image.merge('RGBA', [Image.new('L', (w, w), 5)] * 3 + [shadow]))

    # Контур — тёмная бирюза.
    outline = Image.new('L', (w, w), 0)
    od = ImageDraw.Draw(outline)
    od.polygon(scaled(SHAPE, K), fill=255)
    outline = outline.filter(ImageFilter.MaxFilter(2 * int(K * 1.4) + 1))
    img.paste(Image.new('RGBA', (w, w), FRAME + (255,)), (0, 0), outline)

    # Две грани, как у кристалла: верхняя светло-сиреневая, нижняя голубая.
    mask = Image.new('L', (w, w), 0)
    ImageDraw.Draw(mask).polygon(scaled(SHAPE, K), fill=255)
    tip, right, left = ARROW
    upper = Image.new('L', (w, w), 0)
    ImageDraw.Draw(upper).polygon([(x * K, y * K) for x, y in (tip, right, NOTCH)], fill=255)
    lower = Image.new('L', (w, w), 0)
    ImageDraw.Draw(lower).polygon([(x * K, y * K) for x, y in (tip, NOTCH, left)], fill=255)
    img.paste(Image.new('RGBA', (w, w), FILL_TOP + (255,)), (0, 0), upper)
    img.paste(Image.new('RGBA', (w, w), FILL_BOTTOM + (255,)), (0, 0), lower)

    # Тонкая светлая кромка по ребру между гранями и блик у кончика.
    hl = Image.new('RGBA', (w, w), (0, 0, 0, 0))
    hd = ImageDraw.Draw(hl)
    hd.line([(tip[0] * K, tip[1] * K), (NOTCH[0] * K, NOTCH[1] * K)], fill=(255, 255, 255, 170), width=int(K * 0.9))
    hd.rectangle([4 * K, 4.2 * K, 6 * K, 6.2 * K], fill=(255, 255, 255, 140))
    hl.putalpha(Image.composite(hl.getchannel('A'), Image.new('L', (w, w), 0), mask))
    img.alpha_composite(hl)

    img.resize((N, N), Image.LANCZOS).save(OUT, optimize=True)
    print('ok')


if __name__ == '__main__':
    main()
