"""Train the bay-occupancy CNN on CNRPark-EXT patches (official day-based train / val / test splits).

Data (git-ignored, ~450 MB): Data/cnrpark/patches/ from
https://github.com/fabiocarrara/deep-parking/releases/download/archive/CNR-EXT-Patches-150x150.zip
(CNRPark+EXT, Amato et al. 2017, ODbL 1.0).

    C:\\python-envs\\sb3\\Scripts\\python.exe Vision/train_occupancy_cnn.py
Writes Vision/models/occupancy_cnn.pt and Vision/models/occupancy_cnn_metrics.json.
"""
import json
import os
import time

import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from PIL import Image
from torch.utils.data import DataLoader, Dataset
from torchvision import transforms

from occupancy_cnn import CLASSES, IMAGE_SIZE, MEAN, STD, OccupancyCNN

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "Data", "cnrpark", "patches")
OUT = os.path.join(ROOT, "Vision", "models")


class PatchDataset(Dataset):
    def __init__(self, split, transform):
        with open(os.path.join(DATA, "LABELS", f"{split}.txt")) as f:
            rows = [line.split() for line in f if line.strip()]
        self.items = [(os.path.join(DATA, "PATCHES", p), int(y)) for p, y in rows]
        self.transform = transform

    def __len__(self):
        return len(self.items)

    def __getitem__(self, i):
        path, y = self.items[i]
        return self.transform(Image.open(path).convert("RGB")), y


train_tf = transforms.Compose([
    transforms.Resize((IMAGE_SIZE, IMAGE_SIZE)),
    transforms.RandomHorizontalFlip(),
    transforms.ColorJitter(brightness=0.3, contrast=0.3, saturation=0.2),
    transforms.ToTensor(),
    transforms.Normalize(MEAN, STD),
])
test_tf = transforms.Compose([
    transforms.Resize((IMAGE_SIZE, IMAGE_SIZE)),
    transforms.ToTensor(),
    transforms.Normalize(MEAN, STD),
])


@torch.no_grad()
def evaluate(model, loader):
    model.eval()
    conf = np.zeros((2, 2), dtype=int)  # [true, predicted]
    for x, y in loader:
        pred = model(x).argmax(1)
        for t, p in zip(y.numpy(), pred.numpy()):
            conf[t, p] += 1
    acc = np.trace(conf) / conf.sum()
    return acc, conf


def main(epochs=6, batch_size=256, lr=1e-3, seed=0):
    torch.manual_seed(seed)
    torch.set_num_threads(max(1, os.cpu_count() // 2))
    os.makedirs(OUT, exist_ok=True)
    loaders = {
        "train": DataLoader(PatchDataset("train", train_tf), batch_size=batch_size, shuffle=True, num_workers=6,
                            persistent_workers=True),
        "val": DataLoader(PatchDataset("val", test_tf), batch_size=512, num_workers=4, persistent_workers=True),
        "test": DataLoader(PatchDataset("test", test_tf), batch_size=512, num_workers=4),
    }
    print({k: len(v.dataset) for k, v in loaders.items()}, flush=True)

    model = OccupancyCNN()
    criterion = nn.CrossEntropyLoss()
    optimizer = optim.Adam(model.parameters(), lr=lr)
    history, best_val = [], -1.0
    for epoch in range(1, epochs + 1):
        model.train()
        t0, total, correct, loss_sum = time.time(), 0, 0, 0.0
        for x, y in loaders["train"]:
            optimizer.zero_grad()
            out = model(x)
            loss = criterion(out, y)
            loss.backward()
            optimizer.step()
            loss_sum += loss.item() * len(y)
            correct += (out.argmax(1) == y).sum().item()
            total += len(y)
        val_acc, _ = evaluate(model, loaders["val"])
        history.append({"epoch": epoch, "train_loss": loss_sum / total, "train_acc": correct / total, "val_acc": val_acc})
        print(f"Epoch {epoch}: loss {loss_sum / total:.4f}, train acc {correct / total:.4f}, "
              f"val acc {val_acc:.4f} ({time.time() - t0:.0f} s)", flush=True)
        if val_acc > best_val:
            best_val = val_acc
            torch.save(model.state_dict(), os.path.join(OUT, "occupancy_cnn.pt"))

    model.load_state_dict(torch.load(os.path.join(OUT, "occupancy_cnn.pt")))
    test_acc, conf = evaluate(model, loaders["test"])
    tn, fp, fn, tp = conf[0, 0], conf[0, 1], conf[1, 0], conf[1, 1]
    metrics = {
        "dataset": "CNRPark-EXT patches, official day-based splits",
        "classes": CLASSES, "image_size": IMAGE_SIZE, "epochs": epochs, "best_val_acc": best_val,
        "test_acc": test_acc, "test_precision_occupied": tp / max(1, tp + fp), "test_recall_occupied": tp / max(1, tp + fn),
        "test_confusion_true_by_pred": conf.tolist(), "history": history,
        "parameters": sum(p.numel() for p in model.parameters()),
    }
    with open(os.path.join(OUT, "occupancy_cnn_metrics.json"), "w") as f:
        json.dump(metrics, f, indent=2, default=float)
    print(f"Test accuracy {test_acc:.4f}  confusion [true x pred] {conf.tolist()}", flush=True)


if __name__ == "__main__":
    main()
