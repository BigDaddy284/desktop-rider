"""Packs rendered frames into sprite sheets and lists them in sprites.json.

Usage:  python pack_sprites.py <frames dir> <Assets/Sprites/Rider> [name ...]

<frames dir>/<Right|Left>/<Name>/0000.png ... are the frames written by trick_animations.py, with info.json beside them.
Needs Pillow. Each sheet is saved as a 256-colour PNG with transparency, eight frames to a row, like the existing sheets.
"""
import glob, json, os, sys
from PIL import Image

STD_W, STD_H, COLUMNS = 467, 389, 8
# Room around the standard frame as rendered, and as stored. The stored values are multiples of 20 so that at the
# normal on-screen size (45%) the larger frame lines up with the standard one on a whole pixel.
RENDERED = {'air': dict(left=180, right=260, up=456), 'ground': dict(left=180, right=260, up=100)}
STORED = {'air': dict(left=180, right=260, up=460), 'ground': dict(left=180, right=260, up=100)}


def pack(frames_dir, side, name):
    d = os.path.join(frames_dir, side, name)
    info = json.load(open(os.path.join(d, 'info.json')))
    files = sorted(glob.glob(os.path.join(d, '[0-9]*.png')))
    if len(files) != info['count']:
        raise SystemExit('%s %s: %d frames rendered, %d expected' % (side, name, len(files), info['count']))
    r, st = RENDERED[info['stage']], STORED[info['stage']]
    w = STD_W + st['left'] + st['right']; h = STD_H + st['up']
    rows = (len(files) + COLUMNS - 1) // COLUMNS
    sheet = Image.new('RGBA', (w * COLUMNS, h * rows), (0, 0, 0, 0))
    for i, f in enumerate(files):
        im = Image.open(f).convert('RGBA')
        if im.size != (STD_W + r['left'] + r['right'], STD_H + r['up']):
            raise SystemExit('%s has the wrong size %s' % (f, im.size))
        sheet.paste(im, ((i % COLUMNS) * w, (i // COLUMNS) * h + st['up'] - r['up']))
    return sheet.quantize(256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE), len(files), info['stage'], (w, h)


def main(argv):
    frames_dir, assets = argv[0], argv[1]
    names = argv[2:] or sorted(os.path.basename(p) for p in glob.glob(os.path.join(frames_dir, 'Right', '*')) if os.path.isdir(p))
    manifest_path = os.path.join(assets, 'sprites.json')
    manifest = json.load(open(manifest_path))
    for name in names:
        counts = set()
        for side in ('Right', 'Left'):
            sheet, count, stage, (w, h) = pack(frames_dir, side, name)
            sheet.save(os.path.join(assets, side, name + '.png'), optimize=True)
            counts.add(count)
        if len(counts) != 1:
            raise SystemExit('%s: the two directions have different frame counts' % name)
        st = STORED[stage]
        entry = dict(name=name, loop=False, frameCount=count, columns=COLUMNS, right='Right/%s.png' % name, left='Left/%s.png' % name,
                     frameWidth=w, frameHeight=h, originX=st['left'], originY=st['up'])
        manifest['animations'] = [a for a in manifest['animations'] if a['name'] != name] + [entry]
        print(name, count, 'frames', w, 'x', h)
    json.dump(manifest, open(manifest_path, 'w'), indent=2)
    open(manifest_path, 'a').write('\n')


if __name__ == '__main__':
    main(sys.argv[1:])
