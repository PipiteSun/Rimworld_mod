from items import *

CELL = 128


class Ctx:
    """Placement for a multi-rotation building: normalized plan (u,v in 0..1 for the
    south view) mapped onto the screen-space working rectangle for this rotation."""

    def __init__(self, c, rot, rect, z=1.0):
        self.c, self.rot, self.z = c, rot, z
        self.x0, self.y0, self.x1, self.y1 = rect

    def at(self, u, v):
        if self.rot == 'north':
            u, v = 1 - u, 1 - v
        elif self.rot == 'east':
            u, v = 1 - v, u
        elif self.rot == 'west':
            u, v = v, 1 - u
        return (self.x0 + (self.x1 - self.x0) * u, self.y0 + (self.y1 - self.y0) * v)

    def put(self, u, v, fn, *a, z=None, dx=0, dy=0, **kw):
        x, y = self.at(u, v)
        x, y = x + dx, y + dy
        with self.c.zoom(x, y, z or self.z):
            fn(self.c, x, y, *a, **kw)

    @property
    def wide(self):
        return self.rot in ('south', 'north')


def size_for(rot, wc, hc):
    return (wc * CELL, hc * CELL) if rot in ('south', 'north') else (hc * CELL, wc * CELL)


def render_multi(fn, wc, hc, outdir, name, rots=('south', 'north', 'east'), mask=True, seed=11):
    paths = []
    for rot in rots:
        w, h = size_for(rot, wc, hc)
        c = Canvas(w, h, seed=seed)
        fn(c, rot, w, h)
        p = f'{outdir}/{name}_{rot}.png'
        c.save(p, f'{outdir}/{name}_{rot}m.png' if mask else None)
        paths.append(p)
    return paths
