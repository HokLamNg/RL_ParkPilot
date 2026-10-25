"""Export the occupancy CNN to ONNX for Unity (ShowcaseController runs it with the Inference Engine).

    C:\\python-envs\\sb3\\Scripts\\python.exe Vision/export_occupancy_onnx.py [weights.pt]

Input "input" [N, 3, 64, 64] (RGB, normalised (x - 0.5) / 0.5), output "logits" [N, 2] (free, occupied).
Writes Assets/Project/Models/Vision/occupancy_cnn.onnx and checks it against PyTorch with onnxruntime.
"""
import os
import sys

import numpy as np
import onnxruntime as ort
import torch

from occupancy_cnn import IMAGE_SIZE, load_model

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
weights = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Vision", "models", "occupancy_cnn.pt")
out = os.path.join(ROOT, "Assets", "Project", "Models", "Vision", "occupancy_cnn.onnx")
os.makedirs(os.path.dirname(out), exist_ok=True)

model = load_model(weights)
dummy = torch.zeros(1, 3, IMAGE_SIZE, IMAGE_SIZE)
torch.onnx.export(model, dummy, out, opset_version=15, input_names=["input"], output_names=["logits"],
                  dynamic_axes={"input": {0: "batch"}, "logits": {0: "batch"}})

x = np.random.default_rng(0).uniform(-1, 1, (8, 3, IMAGE_SIZE, IMAGE_SIZE)).astype(np.float32)
ref = model(torch.from_numpy(x)).detach().numpy()
got = ort.InferenceSession(out).run(None, {"input": x})[0]
assert np.allclose(ref, got, atol=1e-4), "ONNX export mismatch"
print("Saved", out, "| max difference vs PyTorch", float(np.abs(ref - got).max()))
