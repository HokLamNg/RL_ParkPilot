"""Photo <-> ground geometry for ParkPilot Assist.

Each bay is marked as 4 image points in this order: the two corners of its OPEN end (where a car drives in,
the aisle side), then the two corners at the BACK of the bay, starting behind the second open-end corner:

    open end:  p0 -------- p1
                |          |
    back:      p3 -------- p2

One reference bay of known real size (default 2.5 x 5.0 m) fixes a homography between the photo and the ground
plane (metres), assuming the car park is flat. Every other bay is mapped through it.
Ground frame: x along the reference bay's open end, z from its open end to its back (into the bay).
"""
import math

import cv2
import numpy as np


def order_quad(points, image_shape=None, open_end=None, ends=None):
    """Canonical corner order for a bay marked by 4 clicks in ANY order: open end (p0, p1), then back (p2 behind p1,
    p3 behind p0).

    The corners are first sorted around the bay. The two shorter sides are the ends. The open end (aisle side) is
    `open_end` (0 or 1, index of the end in that sorted order) if given, otherwise the end nearer the image centre:
    in elevated car-park views the aisle usually runs between the rows, so the open ends face the middle of the photo.
    `ends` (0 or 1) overrides which pair of opposite sides are the ends (otherwise the shorter pair IN THE IMAGE;
    far rows are foreshortened, so align_to_reference decides it on the ground instead).
    Returns (quad, chosen_open_end) so callers can flip it (pass 1 - chosen)."""
    p = np.asarray(points, dtype=np.float64).reshape(4, 2)
    c = p.mean(axis=0)
    p = p[np.argsort(np.arctan2(p[:, 1] - c[1], p[:, 0] - c[0]))]          # around the bay
    side = [np.linalg.norm(p[(i + 1) % 4] - p[i]) for i in range(4)]
    s0 = ends if ends is not None else (0 if side[0] + side[2] <= side[1] + side[3] else 1)  # first end side
    ends = [(s0, (s0 + 1) % 4), (s0 + 2, (s0 + 3) % 4)]                      # the two short sides (index pairs)
    if open_end is None:
        centre = (np.array([image_shape[1], image_shape[0]]) / 2.0) if image_shape is not None else c
        mids = [np.linalg.norm((p[a] + p[b]) / 2 - centre) for a, b in ends]
        open_end = int(np.argmin(mids))
    a, b = ends[open_end]
    q0, q1 = p[a], p[b]
    q2, q3 = p[(b + 1) % 4], p[(a - 1) % 4]                                  # back corners behind q1 and q0
    return np.asarray([q0, q1, q2, q3], dtype=np.float32), open_end


def align_to_reference(quads, H, reference, image_shape):
    """Decide each bay's ends ON THE GROUND: of its two pairs of opposite sides, the ends are the pair that makes the
    bay parallel (or anti-parallel) to the reference bay, as rows in a car park are. Perspective makes far bays look
    shallow, so the shorter sides in the image can be the bay's long sides. The open end stays the end nearer the
    photo centre. Returns (new quads, indices that changed)."""
    def forward(q):
        g = to_ground(H, q)
        f = (g[2] + g[3] - g[0] - g[1]) / 2
        return f / max(np.linalg.norm(f), 1e-9)

    ref_f = forward(quads[reference])
    out, changed = [], []
    for i, q in enumerate(quads):
        if i == reference:
            out.append(q)
            continue
        cands = [order_quad(q, image_shape, ends=e)[0] for e in (0, 1)]
        best = max(cands, key=lambda c: abs(float(np.dot(forward(c), ref_f))))
        same_ends = {tuple(np.round(v, 1)) for v in best[:2]} == {tuple(np.round(v, 1)) for v in q[:2]} or \
                    {tuple(np.round(v, 1)) for v in best[2:]} == {tuple(np.round(v, 1)) for v in q[:2]}
        if not same_ends:
            changed.append(i)
            out.append(best)
        else:
            out.append(q)
    return out, changed


def homography_from_reference(quad_img, width_m=2.5, length_m=5.0):
    """3x3 matrix mapping image pixels -> ground metres, from one bay's 4 marked corners."""
    src = np.asarray(quad_img, dtype=np.float32)
    dst = np.array([[0, 0], [width_m, 0], [width_m, length_m], [0, length_m]], dtype=np.float32)
    return cv2.getPerspectiveTransform(src, dst)


def refine_homography(H0, quads_img, width_m=2.5, length_m=5.0, reference=0, parallel=10.0):
    """Least-squares refinement using every marked bay: map each bay so it is as close as possible to a
    width x length rectangle (side lengths and right angles), while keeping the reference bay where it is.
    One bay alone fixes the scale poorly far away from it (perspective); many bays across the photo fix it well.
    `parallel` weights a soft prior that bays face the same way as the reference bay or the opposite way (straight,
    parallel rows, as in most car parks). Set it to 0 for angled or curved layouts."""
    from scipy.optimize import least_squares

    quads = [np.asarray(q, dtype=np.float64) for q in quads_img]
    ref_dst = np.array([[0, 0], [width_m, 0], [width_m, length_m], [0, length_m]], dtype=np.float64)
    h0 = (H0 / H0[2, 2]).reshape(-1)[:8]

    def project(h, pts):
        H = np.append(h, 1.0).reshape(3, 3)
        p = np.c_[pts, np.ones(len(pts))] @ H.T
        return p[:, :2] / p[:, 2:3]

    def residuals(h):
        res = []
        gr = project(h, quads[reference])
        ref_dir = (gr[2] + gr[3] - gr[0] - gr[1]) / 2
        ref_dir /= max(np.linalg.norm(ref_dir), 1e-9)
        for q in quads:
            g = project(h, q)
            fwd = (g[2] + g[3] - g[0] - g[1]) / 2
            fwd /= max(np.linalg.norm(fwd), 1e-9)
            res.append(parallel * (fwd[0] * ref_dir[1] - fwd[1] * ref_dir[0]))  # sin of the angle: 0 if (anti)parallel
            a, b = g[1] - g[0], g[2] - g[3]          # open end, back
            c, d = g[3] - g[0], g[2] - g[1]          # the two long sides
            res += [np.linalg.norm(a) - width_m, np.linalg.norm(b) - width_m,
                    np.linalg.norm(c) - length_m, np.linalg.norm(d) - length_m,
                    np.dot(a, c) / (width_m * length_m), np.dot(a, d) / (width_m * length_m)]
        res += list(2.0 * (project(h, quads[reference]) - ref_dst).reshape(-1))  # anchor the frame
        return np.asarray(res)

    sol = least_squares(residuals, h0, loss="soft_l1", f_scale=0.3)
    return np.append(sol.x, 1.0).reshape(3, 3)


def to_ground(H, pts_img):
    pts = np.asarray(pts_img, dtype=np.float32).reshape(-1, 1, 2)
    return cv2.perspectiveTransform(pts, H).reshape(-1, 2)


def to_image(H, pts_ground):
    pts = np.asarray(pts_ground, dtype=np.float32).reshape(-1, 1, 2)
    return cv2.perspectiveTransform(pts, np.linalg.inv(H)).reshape(-1, 2)


def bay_rectangle(quad_ground):
    """Fit a bay rectangle to 4 ground points (order as above): centre, yaw (deg), width, length.
    Yaw follows Unity: 0 = the parked car faces +z, positive turns towards +x."""
    q = np.asarray(quad_ground, dtype=np.float64)
    open_mid, back_mid = (q[0] + q[1]) / 2, (q[2] + q[3]) / 2
    fwd = back_mid - open_mid
    length = float(np.linalg.norm(fwd))
    width = float((np.linalg.norm(q[1] - q[0]) + np.linalg.norm(q[2] - q[3])) / 2)
    fwd /= max(length, 1e-6)
    yaw = math.degrees(math.atan2(fwd[0], fwd[1]))
    centre = q.mean(axis=0)
    return {"cx": float(centre[0]), "cz": float(centre[1]), "yaw": yaw, "width": width, "length": length}


def crop_box(quad_img, image_shape, lift=0.35, pad=0.1):
    """Axis-aligned crop around a bay for the occupancy CNN. Cars stand above their ground footprint, so the
    box is extended upwards by `lift` of its height (cameras look down from above) and padded on all sides."""
    q = np.asarray(quad_img, dtype=np.float64)
    x0, y0 = q.min(axis=0)
    x1, y1 = q.max(axis=0)
    w, h = x1 - x0, y1 - y0
    x0, x1 = x0 - pad * w, x1 + pad * w
    y0, y1 = y0 - (lift + pad) * h, y1 + pad * h
    H, W = image_shape[:2]
    return int(max(0, x0)), int(max(0, y0)), int(min(W, x1)), int(min(H, y1))
