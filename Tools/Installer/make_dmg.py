"""Образ диска для macOS (.dmg) из сборки Unity — на Windows, без инструментов Apple.

Образ — ISO 9660 с расширением Rock Ridge: macOS открывает его двойным щелчком, как обычный .dmg
(сырые образы дисков с расширением .dmg Mac монтирует сам). Rock Ridge хранит настоящие имена файлов,
права «исполняемый» (без них игра не запустилась бы) и ярлык «Applications» → /Applications,
на который игрок перетаскивает игру.

Без сжатия: сжатый .dmg в формате Apple (UDZO) надёжно делается только на Mac (hdiutil).

Нужна библиотека pycdlib:  python -m pip install pycdlib
Запуск (обычно — из Unity, меню «Ash & Crown → Собрать для macOS»):
  python make_dmg.py --app "Builds/macOS/Ash & Crown.app" --out "Builds/Installer/AshAndCrown-0.9.0-macOS.dmg"
                     --volume "Ash & Crown" [--add файл ...]
"""
import argparse
import os
import sys

import pycdlib

DIR_MODE = 0o040755
FILE_MODE = 0o100644
EXEC_MODE = 0o100755


def is_exec(rel):
    """Исполняемые: бинарник в Contents/MacOS, библиотеки .dylib, плагины .bundle (если это файл)."""
    return '/Contents/MacOS/' in rel or rel.endswith('.dylib') or rel.endswith('.bundle')


class Builder:
    def __init__(self, volume):
        self.iso = pycdlib.PyCdlib()
        # Уровень 3 + Rock Ridge 1.09: имена в ISO служебные (D0001, F0001), настоящие — в Rock Ridge.
        self.iso.new(interchange_level=3, rock_ridge='1.09', vol_ident=self.vol_ident(volume))
        self.count = 0

    @staticmethod
    def vol_ident(volume):
        # Метка тома в ISO — только A–Z, 0–9 и _: «Ash & Crown» → ASH_CROWN.
        out = ''.join(c if c.isalnum() and c.isascii() else '_' for c in volume.upper())
        while '__' in out:
            out = out.replace('__', '_')
        return out.strip('_')[:32] or 'GAME'

    def next_name(self, prefix):
        self.count += 1
        return f'{prefix}{self.count:06d}'

    def add_dir(self, parent_iso, name):
        iso_path = f'{parent_iso.rstrip("/")}/{self.next_name("D")}'
        self.iso.add_directory(iso_path, rr_name=name, file_mode=DIR_MODE)
        return iso_path

    def add_file(self, parent_iso, name, src, mode):
        iso_path = f'{parent_iso.rstrip("/")}/{self.next_name("F")}.;1'
        self.iso.add_file(src, iso_path, rr_name=name, file_mode=mode)

    def add_tree(self, src_dir, parent_iso, rel_parent=''):
        """Папка целиком (например, Ash & Crown.app) с подпапками. rel — путь внутри образа, для прав."""
        name = os.path.basename(src_dir.rstrip('/\\'))
        rel_dir = f'{rel_parent}/{name}'
        iso_dir = self.add_dir(parent_iso, name)
        for entry in sorted(os.listdir(src_dir)):
            full = os.path.join(src_dir, entry)
            if os.path.isdir(full):
                self.add_tree(full, iso_dir, rel_dir)
            else:
                self.add_file(iso_dir, entry, full, EXEC_MODE if is_exec(f'{rel_dir}/{entry}') else FILE_MODE)

    def add_applications_link(self):
        self.iso.add_symlink(symlink_path=f'/{self.next_name("L")}.;1', rr_symlink_name='Applications',
                             rr_path='/Applications')

    def write(self, out):
        tmp = out + '.tmp'
        self.iso.write(tmp)
        self.iso.close()
        if os.path.exists(out):
            os.remove(out)
        os.replace(tmp, out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--app', required=True, help='папка .app из сборки Unity')
    ap.add_argument('--out', required=True, help='куда записать .dmg')
    ap.add_argument('--volume', default='Ash & Crown')
    ap.add_argument('--add', action='append', default=[], help='файл рядом с игрой (можно несколько)')
    a = ap.parse_args()

    if not os.path.isdir(a.app):
        sys.exit(f'Нет папки приложения: {a.app}')
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)

    b = Builder(a.volume)
    b.add_tree(a.app, "/")
    b.add_applications_link()
    for f in a.add:
        b.add_file('/', os.path.basename(f), f, FILE_MODE)
    b.write(a.out)
    print(f'Образ готов: {a.out} ({os.path.getsize(a.out) / 1024 / 1024:.0f} МБ)')


if __name__ == '__main__':
    main()
