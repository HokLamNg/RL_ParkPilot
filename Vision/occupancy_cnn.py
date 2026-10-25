"""Parking-bay occupancy classifier: a small CNN in the style of the Week 9 notebook (Simple Custom CNN, PyTorch).

Input: a crop of one parking bay, resized to 64 x 64 RGB. Output: 2 logits (0 = free, 1 = occupied).
"""
import torch
import torch.nn as nn
from torchvision import transforms

IMAGE_SIZE = 64
CLASSES = ("free", "occupied")
MEAN, STD = (0.5, 0.5, 0.5), (0.5, 0.5, 0.5)


class OccupancyCNN(nn.Module):
    """3 x (conv 3x3 + ReLU + max-pool 2x2), then two fully connected layers."""

    def __init__(self):
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(3, 32, kernel_size=3, padding=1), nn.ReLU(), nn.MaxPool2d(2, 2),   # 64 -> 32
            nn.Conv2d(32, 64, kernel_size=3, padding=1), nn.ReLU(), nn.MaxPool2d(2, 2),  # 32 -> 16
            nn.Conv2d(64, 128, kernel_size=3, padding=1), nn.ReLU(), nn.MaxPool2d(2, 2), # 16 -> 8
        )
        self.classifier = nn.Sequential(
            nn.Flatten(),
            nn.Linear(128 * 8 * 8, 128), nn.ReLU(), nn.Dropout(0.3),
            nn.Linear(128, len(CLASSES)),
        )

    def forward(self, x):
        return self.classifier(self.features(x))


# Same preprocessing for training (plus augmentation) and for the app.
eval_transform = transforms.Compose([
    transforms.ToPILImage(),
    transforms.Resize((IMAGE_SIZE, IMAGE_SIZE)),
    transforms.ToTensor(),
    transforms.Normalize(MEAN, STD),
])


def load_model(path, device="cpu"):
    model = OccupancyCNN()
    model.load_state_dict(torch.load(path, map_location=device))
    return model.eval()


@torch.no_grad()
def classify_crops(model, crops_rgb):
    """crops_rgb: list of HxWx3 uint8 RGB arrays. Returns (labels, occupied probabilities)."""
    if not crops_rgb:
        return [], []
    x = torch.stack([eval_transform(c) for c in crops_rgb])
    p = torch.softmax(model(x), dim=1)[:, 1]
    return [int(v > 0.5) for v in p], [float(v) for v in p]
