"""Fine-tune the occupancy CNN on Unity car-park patches (ShowcaseController, key G) mixed with real CNRPark patches,
so it works on the showcase's rendered car park without losing accuracy on real photos.

    C:\\python-envs\\sb3\\Scripts\\python.exe Vision/finetune_occupancy_unity.py

Data: Data/unity_patches/labels.txt (path label layout) and Data/cnrpark/patches (real).
Split: Unity patches by car-park layout (every 7th layout held out), real patches by the dataset's day-based split.
Writes Vision/models/occupancy_cnn_unity.pt, its metrics, and exports it to Assets/Project/Models/Vision/occupancy_cnn.onnx.
"""
import json
import os
import random
import subprocess
import sys
import time

import torch
import torch.nn as nn
import torch.optim as optim
from PIL import Image
from torch.utils.data import ConcatDataset, DataLoader, Dataset

from occupancy_cnn import load_model
from train_occupancy_cnn import PatchDataset, evaluate, test_tf, train_tf

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.path.join(ROOT, "Data", "unity_patches")
OUT = os.path.join(ROOT, "Vision", "models")


class UnityPatches(Dataset):
    def __init__(self, rows, transform):
        self.rows, self.transform = rows, transform

    def __len__(self):
        return len(self.rows)

    def __getitem__(self, i):
        path, y = self.rows[i]
        return self.transform(Image.open(os.path.join(UNITY, path)).convert("RGB")), y


def main(epochs=4, real_train=30000, seed=0):
    random.seed(seed)
    torch.manual_seed(seed)
    torch.set_num_threads(max(1, os.cpu_count() // 2))
    rows = [line.split() for line in open(os.path.join(UNITY, "labels.txt")) if line.strip()]
    train_rows = [(p, int(y)) for p, y, n in rows if int(n) % 7 != 0]
    val_rows = [(p, int(y)) for p, y, n in rows if int(n) % 7 == 0]
    print(f"Unity patches: {len(train_rows)} train, {len(val_rows)} validation "
          f"({sum(1 for _, y in train_rows if y == 0)} / {sum(1 for _, y in val_rows if y == 0)} free)", flush=True)

    real = PatchDataset("train", train_tf)
    real.items = random.sample(real.items, min(real_train, len(real.items)))
    train = ConcatDataset([UnityPatches(train_rows, train_tf), real])
    loaders = {
        "train": DataLoader(train, batch_size=256, shuffle=True, num_workers=6, persistent_workers=True),
        "unity_val": DataLoader(UnityPatches(val_rows, test_tf), batch_size=512, num_workers=2),
        "real_test": DataLoader(PatchDataset("test", test_tf), batch_size=512, num_workers=4),
    }

    model = load_model(os.path.join(OUT, "occupancy_cnn.pt"))
    before_unity, conf_unity_before = evaluate(model, loaders["unity_val"])
    print(f"Before fine-tuning: Unity validation accuracy {before_unity:.4f} {conf_unity_before.tolist()}", flush=True)

    criterion = nn.CrossEntropyLoss()
    optimizer = optim.Adam(model.parameters(), lr=3e-4)
    for epoch in range(1, epochs + 1):
        model.train()
        t0, loss_sum, n = time.time(), 0.0, 0
        for x, y in loaders["train"]:
            optimizer.zero_grad()
            loss = criterion(model(x), y)
            loss.backward()
            optimizer.step()
            loss_sum += loss.item() * len(y)
            n += len(y)
        acc, _ = evaluate(model, loaders["unity_val"])
        print(f"Epoch {epoch}: loss {loss_sum / n:.4f}, Unity validation accuracy {acc:.4f} ({time.time() - t0:.0f} s)", flush=True)

    unity_acc, unity_conf = evaluate(model, loaders["unity_val"])
    real_acc, real_conf = evaluate(model, loaders["real_test"])
    weights = os.path.join(OUT, "occupancy_cnn_unity.pt")
    torch.save(model.state_dict(), weights)
    metrics = {"unity_val_acc_before": before_unity, "unity_val_acc": unity_acc, "unity_val_confusion_true_by_pred": unity_conf.tolist(),
               "real_test_acc": real_acc, "real_test_confusion_true_by_pred": real_conf.tolist(),
               "unity_train_patches": len(train_rows), "unity_val_patches": len(val_rows), "real_train_patches": len(real.items),
               "epochs": epochs}
    with open(os.path.join(OUT, "occupancy_cnn_unity_metrics.json"), "w") as f:
        json.dump(metrics, f, indent=2, default=float)
    print(f"Unity validation {unity_acc:.4f} (before {before_unity:.4f}) | real test {real_acc:.4f} (base model 0.9904)", flush=True)

    here = os.path.dirname(os.path.abspath(__file__))
    subprocess.run([sys.executable, os.path.join(here, "export_occupancy_onnx.py"), weights], check=True, cwd=here)


if __name__ == "__main__":
    main()
