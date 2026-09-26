"""
A yellow "Caution: floor slippery when wet" A-frame sign, about waist high.

Two panels lean against each other from a rounded bar along the top, like a
tent. Each panel has a handle hole under the bar, a recessed field with a
raised rim round it, a printed label on its outer face (its own surface,
"Label", so the warning can be painted on later) and two feet at its bottom
corners with a notch between them. Reference: docs/reference/WetFloorSign.jpg.

Each panel is drawn hanging straight down from the top bar and then leaned
out round the bar, the front one forwards and the back one backwards, so the
same numbers draw both.
"""

from math import cos, radians

from paniq_models import Model

HEIGHT = 0.90               # top of the bar: about waist high
BAR_RADIUS = 0.025
HINGE = HEIGHT - BAR_RADIUS  # the line both panels hang from
LEAN_DEGREES = 13.0          # each panel off upright; the feet end up ~45 cm apart
LENGTH = HINGE / cos(radians(LEAN_DEGREES))  # along the panel, so its inner bottom edge meets the floor
WIDTH = 0.42
THICK = 0.02

HANDLE_WIDTH = 0.22
HANDLE_TOP = 0.04            # below the hinge, measured along the panel
HANDLE_BOTTOM = 0.16
FOOT_WIDTH = 0.08
FOOT_HEIGHT = 0.04
RIM = 0.025
RECESS = 0.004
LABEL_WIDTH = WIDTH - 2 * RIM - 0.04
LABEL_TOP = 0.20
LABEL_BOTTOM = 0.62
LABEL_THICK = 0.002


def build():
    m = Model("WetFloorSign", footprint_mm=(420, 450), height_mm=900, budget_tris=500)
    m.cylinder("TopBar", radius=BAR_RADIUS, height=WIDTH, at=(0.0, 0.0, HINGE - BAR_RADIUS), axis="X")
    for side, name in ((-1, "Front"), (1, "Back")):
        panel(m, side, name)
    return m


def panel(m, side, name):
    """One leaf of the A: side -1 leans forwards (the front), +1 backwards."""
    outer = "front" if side < 0 else "back"
    mid = side * THICK / 2.0

    def hang(piece_name, width, top, bottom, x=0.0, y=mid, depth=THICK, surface="Body"):
        # A block between ``top`` and ``bottom`` metres down the panel from the hinge.
        return m.box(f"{name}{piece_name}", size=(width, depth, bottom - top),
                     at=(x, y, HINGE - bottom), surface=surface)

    stile = (WIDTH - HANDLE_WIDTH) / 2.0
    pieces = [
        hang("Grip", WIDTH, 0.0, HANDLE_TOP),
        hang("LeftStile", stile, HANDLE_TOP, HANDLE_BOTTOM, x=-(HANDLE_WIDTH + stile) / 2.0),
        hang("RightStile", stile, HANDLE_TOP, HANDLE_BOTTOM, x=(HANDLE_WIDTH + stile) / 2.0),
        hang("Board", WIDTH, HANDLE_BOTTOM, LENGTH - FOOT_HEIGHT).inset(outer, RIM, -RECESS),
        hang("LeftFoot", FOOT_WIDTH, LENGTH - FOOT_HEIGHT, LENGTH, x=-(WIDTH - FOOT_WIDTH) / 2.0),
        hang("RightFoot", FOOT_WIDTH, LENGTH - FOOT_HEIGHT, LENGTH, x=(WIDTH - FOOT_WIDTH) / 2.0),
        hang("Label", LABEL_WIDTH, LABEL_TOP, LABEL_BOTTOM,
             y=side * (THICK - RECESS + LABEL_THICK / 2.0), depth=LABEL_THICK, surface="Label"),
    ]
    for piece in pieces:
        piece.rotate(side * LEAN_DEGREES, "X", about=(0.0, 0.0, HINGE))
