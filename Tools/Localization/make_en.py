"""Собирает английский словарь Assets/Scripts/Game/Localization.En.cs из двух файлов:
keys.txt (русские строки, по одной на строку, как в исходниках — с \\n) и en.txt (перевод, строка в строку).

Запуск из корня проекта:  python Tools/Localization/make_en.py
Ведущие и конечные пробелы перевода подгоняются под ключ (они важны при склейке строк).
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
def read_lines(name):
    # Git на Windows может отдать файл с CRLF — убираем \r, чтобы он не попал в ключи.
    text = open(os.path.join(HERE, name), encoding='utf-8').read().replace('\r', '')
    return text.rstrip('\n').split('\n')


keys = read_lines('keys.txt')
en = read_lines('en.txt')
if len(keys) != len(en):
    raise SystemExit(f'Разное число строк: keys {len(keys)}, en {len(en)}')


def match_spaces(key, value):
    lead = key[:len(key) - len(key.lstrip(' '))]
    trail = key[len(key.rstrip(' ')):]
    return lead + value.strip(' ') + trail


lines = []
seen = set()
for k, v in zip(keys, en):
    if k in seen:
        continue
    seen.add(k)
    v = match_spaces(k, v)
    if '"' in k or '"' in v:
        raise SystemExit('Кавычка в строке: ' + k)
    lines.append(f'        {{ "{k}", "{v}" }},')

out = '''// Создано Tools/Localization/make_en.py — правь keys.txt и en.txt, а не этот файл.
using System.Collections.Generic;

public static partial class L
{
    private static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
''' + '\n'.join(lines) + '''
    };
}
'''
path = os.path.join(HERE, '..', '..', 'Assets', 'Scripts', 'Game', 'Localization.En.cs')
open(path, 'w', encoding='utf-8', newline='\n').write(out)
print(len(lines), 'строк')
