"""ParkPilot Assist: a photo of any car park -> bay occupancy (CNN) -> simulated car park -> trained RL agent parks
-> the recommended manoeuvre drawn on the photo.

    C:\\python-envs\\sb3\\Scripts\\python.exe Vision/assist.py --image <photo.jpg> [--layout Vision/layouts/<camera>.json]

1. Bays: loaded from the layout file, or marked once per camera with 4 clicks each: the two corners of the bay's open
   end (aisle side), then the two back corners (see Vision/geometry.py). Mark a clearly visible bay first: it is the
   reference that fixes the photo-to-ground scale (default 2.5 x 5.0 m, --bay-size to change).
2. The occupancy CNN (Vision/occupancy_cnn.py) classifies every bay: green = free, red = occupied.
3. Click a FREE bay to choose the target. 1 / 2 = start driving along the aisle / facing the bay. Enter writes
   Scenarios/scenario.json: every bay in metres plus the target.
4. Unity (ScenarioRunner, Training scene) builds that car park, the trained agent parks, and Unity writes
   Scenarios/result.json with the path in metres. The app draws the path back onto the photo and saves
   Scenarios/assist_<photo>.png (photo with the manoeuvre | top-down simulation).
"""
import argparse
import json
import os
import time

import cv2
import numpy as np

from geometry import (align_to_reference, bay_rectangle, crop_box, homography_from_reference, order_quad,
                      refine_homography, to_ground, to_image)
from occupancy_cnn import classify_crops, load_model

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENARIOS = os.path.join(ROOT, "Scenarios")
MODEL = os.path.join(ROOT, "Vision", "models", "occupancy_cnn.pt")
STARTS = {"1": "aisle", "2": "facing"}
START_TEXT = {"aisle": "driving along the aisle", "facing": "facing the bay"}

GREEN, RED, YELLOW, WHITE, BLACK, BLUE, ORANGE = (60, 200, 60), (40, 40, 230), (0, 220, 255), (255, 255, 255), \
    (0, 0, 0), (255, 140, 30), (0, 140, 255)


# ---------------------------------------------------------------------------- pipeline steps

def load_layout(path):
    with open(path) as f:
        d = json.load(f)
    return [np.asarray(q, dtype=np.float32) for q in d["bays"]], d.get("reference", 0), d.get("bay_size", [2.5, 5.0])


def save_layout(path, quads, reference, bay_size, note=""):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump({"note": note or "Bay corners in image pixels: open end (2), then back (2).",
                   "reference": reference, "bay_size": bay_size,
                   "bays": [[[round(float(v), 1) for v in p] for p in q] for q in quads]}, f, indent=1)


def flip_open_end(q):
    """Same bay, the other short side as its open end."""
    a, _ = order_quad(q, open_end=0)
    b, _ = order_quad(q, open_end=1)
    same = lambda x, y: {tuple(np.round(v, 1)) for v in x[:2]} == {tuple(np.round(v, 1)) for v in y[:2]}
    return b if same(a, q) else a


def sideways_bays(quads, H):
    """Bays that come out wider than long on the ground: almost always a wrongly chosen open end."""
    out = []
    for i, q in enumerate(quads):
        r = bay_rectangle(to_ground(H, q))
        if r["width"] > 0.8 * r["length"]:
            out.append(i)
    return out


def classify(model, image_bgr, quads):
    crops = []
    for q in quads:
        x0, y0, x1, y1 = crop_box(q, image_bgr.shape)
        crops.append(cv2.cvtColor(image_bgr[y0:y1, x0:x1], cv2.COLOR_BGR2RGB))
    return classify_crops(model, crops)


def ground_homography(quads, reference, bay_size, image_shape):
    """Photo -> ground metres: from the reference bay, then every other bay's ends are decided on the ground (parallel
    to the reference), then refined with every marked bay (all assumed standard size). Returns (H, quads, changed)."""
    H = homography_from_reference(quads[reference], *bay_size)
    quads, changed = align_to_reference(quads, H, reference, image_shape)
    if len(quads) >= 3:
        H = refine_homography(H, quads, *bay_size, reference=reference)
    return H, quads, changed


def target_neighbours(bays, target):
    """Bays directly beside the target in metres: same row (facing the same way), about one bay width to the side."""
    import math
    t = bays[target]
    yaw = math.radians(t["yaw"])
    fwd = np.array([math.sin(yaw), math.cos(yaw)])
    right = np.array([math.cos(yaw), -math.sin(yaw)])
    out = []
    for i, b in enumerate(bays):
        if i == target:
            continue
        dyaw = abs((b["yaw"] - t["yaw"] + 180) % 360 - 180)
        d = np.array([b["cx"] - t["cx"], b["cz"] - t["cz"]])
        across, along = abs(d @ right), abs(d @ fwd)
        if dyaw < 20 and 0.5 * t["width"] < across < 1.6 * t["width"] and along < 0.5 * t["length"]:
            out.append(i)
    return out


def build_scenario(quads, occ, target, start_mode, H, image_path, image_shape, bay_size, occupancy="cnn"):
    """occupancy: "cnn" = as classified; "neighbours" = only the target's direct neighbours keep their CNN state
    (every other bay empty); "none" = every bay empty. The last two are diagnostics (how much do the other cars matter)."""
    bays = []
    for i, q in enumerate(quads):
        r = bay_rectangle(to_ground(H, q))
        r.update(id=str(i), occupied=bool(occ[i]) and i != target)
        bays.append(r)
    if occupancy != "cnn":
        keep = set(target_neighbours(bays, target)) if occupancy == "neighbours" else set()
        for i, b in enumerate(bays):
            if i not in keep:
                b["occupied"] = False
    return {"bays": bays, "target_index": int(target), "start_mode": start_mode, "metric_bay_width": bay_size[0],
            "occupancy_mode": occupancy,
            "source_image": os.path.abspath(image_path), "image_size": [image_shape[1], image_shape[0]],
            "homography_image_to_ground": H.tolist()}


def layout_preview(scenario, path_xz=None, px_per_m=24):
    """Top-down drawing of the layout in metres (what Unity builds), optionally with the agent's path."""
    import math
    bays = scenario["bays"]
    pts = []
    for b in bays:
        yaw = math.radians(b["yaw"])
        f, r = np.array([math.sin(yaw), math.cos(yaw)]), np.array([math.cos(yaw), -math.sin(yaw)])
        c = np.array([b["cx"], b["cz"]])
        b["_corners"] = [c + r * sx * b["width"] / 2 + f * sz * b["length"] / 2 for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        pts += b["_corners"]
    pts = np.array(pts)
    lo, hi = pts.min(0) - 3, pts.max(0) + 3
    W, Hh = ((hi - lo) * px_per_m).astype(int)
    img = np.full((Hh, W, 3), 60, np.uint8)
    to_px = lambda p: (int((p[0] - lo[0]) * px_per_m), int((hi[1] - p[1]) * px_per_m))  # +z up
    for i, b in enumerate(bays):
        col = YELLOW if i == scenario["target_index"] else (RED if b["occupied"] else GREEN)
        poly = np.array([to_px(p) for p in b.pop("_corners")], np.int32)
        cv2.polylines(img, [poly], True, col, 2, cv2.LINE_AA)
        cv2.line(img, tuple(poly[0]), tuple(poly[1]), col, 4, cv2.LINE_AA)
        cv2.putText(img, str(i), tuple(poly.mean(0).astype(int)), cv2.FONT_HERSHEY_SIMPLEX, 0.45, WHITE, 1)
    if path_xz is not None and len(path_xz) > 1:
        cv2.polylines(img, [np.array([to_px(p) for p in path_xz], np.int32)], False, BLUE, 3, cv2.LINE_AA)
    cv2.putText(img, "1 m grid: " + str(px_per_m) + " px", (6, Hh - 8), cv2.FONT_HERSHEY_SIMPLEX, 0.4, WHITE, 1)
    return img


def draw_path_on_photo(image, H, path_xz):
    """Project the agent's path (ground metres) back into the photo."""
    vis = image.copy()
    if len(path_xz) < 2:
        return vis
    pts = to_image(H, np.asarray(path_xz, dtype=np.float32))
    poly = pts.round().astype(np.int32).reshape(-1, 1, 2)
    cv2.polylines(vis, [poly], False, BLACK, 7, cv2.LINE_AA)
    cv2.polylines(vis, [poly], False, BLUE, 4, cv2.LINE_AA)
    cv2.circle(vis, tuple(int(v) for v in pts[0]), 9, ORANGE, -1, cv2.LINE_AA)
    cv2.circle(vis, tuple(int(v) for v in pts[-1]), 9, YELLOW, -1, cv2.LINE_AA)
    return vis


# ---------------------------------------------------------------------------- drawing

def draw(image, quads, occ=None, prob=None, target=None, reference=0, msg="", scale=1.0, partial=None):
    vis = image.copy()
    for i, q in enumerate(quads):
        c = WHITE if occ is None else (RED if occ[i] else GREEN)
        if i == target:
            c = YELLOW
        poly = q.round().astype(np.int32).reshape(-1, 1, 2)
        cv2.polylines(vis, [poly], True, c, 4 if i == target else 2, cv2.LINE_AA)
        cv2.line(vis, tuple(int(v) for v in q[0]), tuple(int(v) for v in q[1]), c, 5, cv2.LINE_AA)  # open end
        label = f"{i}{'*' if i == reference else ''}" + ("" if prob is None else f" {prob[i]:.2f}")
        org = (int(q[:, 0].min()) + 4, int(q[:, 1].min()) + 16)
        cv2.putText(vis, label, org, cv2.FONT_HERSHEY_SIMPLEX, 0.5, BLACK, 3, cv2.LINE_AA)
        cv2.putText(vis, label, org, cv2.FONT_HERSHEY_SIMPLEX, 0.5, c, 1, cv2.LINE_AA)
    for p in partial or []:
        cv2.circle(vis, tuple(int(v) for v in p), 5, YELLOW, -1)
    if scale != 1.0:
        vis = cv2.resize(vis, None, fx=scale, fy=scale)
    lines = msg.split("\n") if msg else []
    if lines:
        cv2.rectangle(vis, (0, 0), (vis.shape[1], 22 * len(lines) + 8), BLACK, -1)
    for k, line in enumerate(lines):
        cv2.putText(vis, line, (8, 20 + 22 * k), cv2.FONT_HERSHEY_SIMPLEX, 0.55, WHITE, 1, cv2.LINE_AA)
    return vis


def composite(photo_vis, result_png, result):
    sim = cv2.imread(result_png)
    h = 720
    a = cv2.resize(photo_vis, (int(photo_vis.shape[1] * h / photo_vis.shape[0]), h))
    b = cv2.resize(sim, (h, h)) if sim is not None else np.zeros((h, h, 3), np.uint8)
    out = np.hstack([a, b])
    text = (f"Recommended manoeuvre: {result['outcome']} in {result['seconds']:.1f} s, {result['finish_direction']}, "
            f"{result['final_distance_m']:.2f} m from bay centre, heading error {result['final_heading_error_deg']:.1f} deg")
    cv2.rectangle(out, (0, h - 34), (out.shape[1], h), BLACK, -1)
    cv2.putText(out, text, (10, h - 11), cv2.FONT_HERSHEY_SIMPLEX, 0.6, WHITE, 1, cv2.LINE_AA)
    return out


def composite_batch(photo_vis, batch_png, b):
    sim = cv2.imread(batch_png)
    h = 720
    a = cv2.resize(photo_vis, (int(photo_vis.shape[1] * h / photo_vis.shape[0]), h))
    bimg = cv2.resize(sim, (h, h)) if sim is not None else np.zeros((h, h, 3), np.uint8)
    out = np.hstack([a, bimg])
    rate = 100 * b["success_rate"]
    text = (f"Batch test, {b['model']}: parked {b['parked']}/{b['runs']} ({rate:.0f}%)"
            + (f", parked runs: {b['parked_mean_seconds']:.1f} s, {b['parked_mean_heading_error_deg']:.1f} deg"
               if b.get("parked_mean_seconds") is not None else "")
            + "   (green = parked, orange = stuck, red = collision)")
    cv2.rectangle(out, (0, h - 34), (out.shape[1], h), BLACK, -1)
    cv2.putText(out, text, (10, h - 11), cv2.FONT_HERSHEY_SIMPLEX, 0.6, WHITE, 1, cv2.LINE_AA)
    return out


def finish(image, quads, occ, prob, target, reference, H, result, name):
    photo = draw(draw_path_on_photo(image, H, result.get("path_xz", [])), quads, occ, prob, target, reference)
    out = composite(photo, os.path.join(SCENARIOS, "result.png"), result)
    path = os.path.join(SCENARIOS, f"assist_{name}.png")
    cv2.imwrite(path, out)
    print("Result:", json.dumps({k: v for k, v in result.items() if k != "path_xz"}))
    print("Saved", path)
    return out


def result_after(t):
    path = os.path.join(SCENARIOS, "result.json")
    if os.path.exists(path) and os.path.getmtime(path) > t:
        time.sleep(0.3)  # let Unity finish writing the PNG
        with open(path) as f:
            return json.load(f)
    return None


# ---------------------------------------------------------------------------- interactive marking

def mark_bays(win, image, scale, quads):
    quads = [q.copy() for q in quads]
    pts = []

    def on_mouse(event, x, y, flags, param):
        if event == cv2.EVENT_LBUTTONDOWN:
            pts.append((x / scale, y / scale))
            if len(pts) == 4:
                q, _ = order_quad(pts, image.shape)   # any click order; open end guessed (nearer the photo centre)
                quads.append(q)
                pts.clear()

    cv2.setMouseCallback(win, on_mouse)
    while True:
        msg = (f"MARK BAYS ({len(quads)} marked). Click the 4 painted corners of a bay, any order ({len(pts)}/4).  "
               f"Mark a clear bay first (= scale reference).\nThick edge = open end (aisle side): F = flip it for the last bay.   "
               f"U = undo   S = save and continue   Q = quit")
        cv2.imshow(win, draw(image, quads, msg=msg, scale=scale, partial=pts))
        k = cv2.waitKey(20) & 0xFF
        if k in (ord("f"), ord("F")) and quads and not pts:
            quads[-1] = flip_open_end(quads[-1])
        if k in (ord("u"), ord("U")):
            if pts:
                pts.pop()
            elif quads:
                quads.pop()
        elif k in (ord("s"), ord("S")) and quads:
            return quads
        elif k in (ord("q"), ord("Q"), 27):
            raise SystemExit


# ---------------------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--image", required=True)
    ap.add_argument("--layout", help="bay layout JSON (default: Vision/layouts/<image name>.json)")
    ap.add_argument("--bay-size", nargs=2, type=float, metavar=("WIDTH", "LENGTH"),
                    help="real size of the reference bay in metres (default from the layout, else 2.5 5.0)")
    ap.add_argument("--target", type=int, help="non-interactive: index of the target bay")
    ap.add_argument("--start", default="aisle", choices=["aisle", "facing"], help="non-interactive: start pose")
    ap.add_argument("--wait", action="store_true", help="non-interactive: wait for Unity and save the result image")
    ap.add_argument("--occupancy", default="cnn", choices=["cnn", "neighbours", "none"],
                    help="diagnostic: cnn = as classified (default); neighbours = only the target's direct neighbours "
                         "keep their state, all other bays empty; none = empty car park")
    ap.add_argument("--no-trim", action="store_true",
                    help="diagnostic: keep the target bay's full measured length (default: trimmed to the trained 4.88 m)")
    ap.add_argument("--back-barriers", action="store_true",
                    help="diagnostic: a 1 m barrier along the back of every bay (the agent's sensors see it like a wall)")
    ap.add_argument("--no-early-endings", action="store_true",
                    help="no stuck / no-progress endings in Unity (training shortcuts); the agent gets the full 30 s")
    ap.add_argument("--trace", action="store_true",
                    help="diagnostic: Unity writes one CSV row per decision (observations, actions, rays, shaping terms)")
    ap.add_argument("--batch", action="store_true",
                    help="non-interactive: ask Unity for the batch test (the same scenario from about 24 starts)")
    args = ap.parse_args()

    image = cv2.imread(args.image)
    if image is None:
        raise SystemExit(f"Cannot read {args.image}")
    name = os.path.splitext(os.path.basename(args.image))[0]
    layout_path = args.layout or os.path.join(ROOT, "Vision", "layouts", name + ".json")
    quads, reference, bay_size = load_layout(layout_path) if os.path.exists(layout_path) else ([], 0, [2.5, 5.0])
    if args.bay_size:
        bay_size = list(args.bay_size)
    model = load_model(MODEL)
    os.makedirs(SCENARIOS, exist_ok=True)

    if args.target is not None:  # non-interactive
        occ, prob = classify(model, image, quads)
        if occ[args.target]:
            raise SystemExit(f"Bay {args.target} is classified as occupied ({prob[args.target]:.2f}).")
        H, quads, changed = ground_homography(quads, reference, bay_size, image.shape)
        if changed:
            print(f"Bays {changed}: ends re-chosen on the ground (parallel to the reference bay); layout updated.")
            save_layout(layout_path, quads, reference, bay_size)
        if sideways_bays(quads, H):
            print(f"WARNING: bays {sideways_bays(quads, H)} come out wider than long: check which end is the open end.")
        s = build_scenario(quads, occ, args.target, args.start, H, args.image, image.shape, bay_size, args.occupancy)
        s["batch"] = bool(args.batch)
        s["trim_target"] = not args.no_trim
        s["back_barriers"] = bool(args.back_barriers)
        s["early_endings"] = not args.no_early_endings
        s["trace"] = bool(args.trace)
        t0 = time.time()
        with open(os.path.join(SCENARIOS, "scenario.json"), "w") as f:
            json.dump(s, f, indent=1)
        cv2.imwrite(os.path.join(SCENARIOS, "photo_classified.png"), draw(image, quads, occ, prob, args.target, reference))
        cv2.imwrite(os.path.join(SCENARIOS, "layout_preview.png"), layout_preview(json.loads(json.dumps(s))))
        print(f"Occupancy mode {args.occupancy}: {sum(b['occupied'] for b in s['bays'])} parked cars in the simulation, "
              f"target neighbours {target_neighbours(s['bays'], args.target)}")
        print(json.dumps({"occupied": occ, "prob": [round(p, 3) for p in prob],
                          "bays_m": [{k: round(v, 2) if isinstance(v, float) else v for k, v in b.items()} for b in s["bays"]]}))
        if args.wait:
            print("Waiting for Unity (press Play with the ScenarioRunner enabled, or L if it is running)...")
            if args.batch:
                summary = os.path.join(SCENARIOS, "batch_summary.json")
                while not (os.path.exists(summary) and os.path.getmtime(summary) > t0):
                    time.sleep(0.5)
                time.sleep(1.0)  # the overview image is written just after the summary
                with open(summary) as f:
                    b = json.load(f)
                print("Batch:", json.dumps(b))
                photo = draw(image, quads, occ, prob, args.target, reference)
                out = composite_batch(photo, os.path.join(SCENARIOS, "result_batch.png"), b)
                cv2.imwrite(os.path.join(SCENARIOS, f"assist_batch_{name}.png"), out)
                print("Saved", os.path.join(SCENARIOS, f"assist_batch_{name}.png"))
            else:
                while (r := result_after(t0)) is None:
                    time.sleep(0.5)
                finish(image, quads, occ, prob, args.target, reference, H, r, name)
        return

    win = "ParkPilot Assist"
    cv2.namedWindow(win)
    scale = min(1.0, 1300 / image.shape[1], 850 / image.shape[0])
    if not quads:
        quads = mark_bays(win, image, scale, [])
        reference = 0
        save_layout(layout_path, quads, reference, bay_size)
        print(f"Saved {len(quads)} bays to {layout_path}")

    def analyse():
        nonlocal quads
        H, quads, changed = ground_homography(quads, reference, bay_size, image.shape)
        if changed:
            print(f"Bays {changed}: ends re-chosen on the ground (parallel to the reference bay); layout updated.")
            save_layout(layout_path, quads, reference, bay_size)
        o, p = classify(model, image, quads)
        return o, p, H

    occ, prob, H = analyse()
    state = {"target": None, "flip": None}

    def on_click(event, x, y, flags, param):
        if event not in (cv2.EVENT_LBUTTONDOWN, cv2.EVENT_RBUTTONDOWN):
            return
        pt = (x / scale, y / scale)
        for i, q in enumerate(quads):
            if cv2.pointPolygonTest(q.reshape(-1, 1, 2), pt, False) >= 0:
                if event == cv2.EVENT_RBUTTONDOWN:
                    state["flip"] = i
                elif not occ[i]:
                    state["target"] = i
                return

    cv2.setMouseCallback(win, on_click)
    start_mode, written_at, shown = "aisle", None, None
    while True:
        t = state["target"]
        if shown is not None:
            vis = cv2.resize(shown, None, fx=min(1.0, 1500 / shown.shape[1]), fy=min(1.0, 1500 / shown.shape[1]))
        else:
            free = sum(1 for o in occ if not o)
            if written_at is not None:
                msg = "Scenario written. In Unity press Play (ScenarioRunner enabled), or L if it is already running...\nQ = quit"
            else:
                bad = sideways_bays(quads, H)
                warn = f"   CHECK bays {bad}: wider than long - right-click to flip the open end" if bad else ""
                msg = (f"{len(quads)} bays: {len(quads) - free} occupied (red), {free} free (green). Click a FREE bay = target."
                       f"  Right-click = flip open end.{warn}"
                       f"\nStart: {START_TEXT[start_mode]} (1 = along aisle, 2 = facing bay)   Enter = park   "
                       f"M = re-mark bays   Q = quit")
            vis = draw(image, quads, occ, prob, t, reference, msg=msg, scale=scale)
        cv2.imshow(win, vis)
        k = cv2.waitKey(50) & 0xFF
        if k in (ord("q"), ord("Q"), 27):
            break
        if chr(k) in STARTS:
            start_mode = STARTS[chr(k)]
        if state["flip"] is not None:
            i, state["flip"] = state["flip"], None
            quads[i] = flip_open_end(quads[i])
            save_layout(layout_path, quads, reference, bay_size)
            occ, prob, H = analyse()
            written_at, shown = None, None
        if k in (ord("m"), ord("M")):
            quads = mark_bays(win, image, scale, quads)
            save_layout(layout_path, quads, reference, bay_size)
            cv2.setMouseCallback(win, on_click)
            occ, prob, H = analyse()
            state["target"], written_at, shown = None, None, None
        if k == 13 and t is not None:  # Enter
            s = build_scenario(quads, occ, t, start_mode, H, args.image, image.shape, bay_size)
            written_at = time.time()
            with open(os.path.join(SCENARIOS, "scenario.json"), "w") as f:
                json.dump(s, f, indent=1)
            shown = None
            print("Wrote", os.path.join(SCENARIOS, "scenario.json"))
        if written_at is not None and shown is None:
            r = result_after(written_at)
            if r is not None:
                shown = finish(image, quads, occ, prob, t, reference, H, r, name)
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
