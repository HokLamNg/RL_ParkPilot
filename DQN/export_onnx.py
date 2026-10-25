"""Export a trained SB3 DQN as an ML-Agents-compatible .onnx, so Unity (EvaluationRunner, Inference Only)
can run it exactly like the PPO and SAC models.

Interface copied from the ML-Agents models in this project (opset 9):
  inputs  obs_0 [batch, 68] (rays), obs_1 [batch, 9] (vector)
  outputs version_number = 3, memory_size = 0, continuous_action_output_shape = 2,
          continuous_actions and deterministic_continuous_actions [batch, 2]
The network picks argmax Q and looks the (steering, throttle) pair up in ACTION_TABLE, so both action
outputs are the greedy action.

Re-export a saved model:
    C:\\python-envs\\sb3\\Scripts\\python.exe DQN/export_onnx.py results/expC_dqn_s1/ParkingAgent/ParkingAgent.zip results/expC_dqn_s1/ParkingAgent.onnx
"""
import sys

import numpy as np
import torch

from unity_vec_env import ACTION_TABLE

OBS_SIZES = (68, 9)


class DQNPolicyForUnity(torch.nn.Module):
    def __init__(self, q_net):
        super().__init__()
        self.q_net = q_net
        self.table = torch.nn.Parameter(
            torch.tensor(ACTION_TABLE), requires_grad=False)
        self.version_number = torch.nn.Parameter(
            torch.tensor([3.0]), requires_grad=False)
        self.memory_size = torch.nn.Parameter(
            torch.tensor([0.0]), requires_grad=False)
        self.action_shape = torch.nn.Parameter(torch.tensor(
            [float(ACTION_TABLE.shape[1])]), requires_grad=False)

    def forward(self, obs_0, obs_1):
        x = torch.cat([obs_0, obs_1], dim=1)
        q = self.q_net(x)
        idx = torch.argmax(q, dim=1)
        actions = torch.index_select(self.table, 0, idx)
        return self.version_number, self.memory_size, actions, self.action_shape, actions


def export_onnx(model, obs_sizes, path):
    assert tuple(
        obs_sizes) == OBS_SIZES, f"unexpected observation sizes {obs_sizes}"
    q_net = model.policy.q_net.eval().cpu()
    wrapper = DQNPolicyForUnity(q_net).eval()
    dummy = (torch.zeros(1, obs_sizes[0]), torch.zeros(1, obs_sizes[1]))
    torch.onnx.export(
        wrapper, dummy, path, opset_version=9,
        input_names=["obs_0", "obs_1"],
        output_names=["version_number", "memory_size", "continuous_actions",
                      "continuous_action_output_shape", "deterministic_continuous_actions"],
        dynamic_axes={"obs_0": {0: "batch"}, "obs_1": {0: "batch"},
                      "continuous_actions": {0: "batch"}, "deterministic_continuous_actions": {0: "batch"}},
    )
    _check(wrapper, path, obs_sizes)


def _check(wrapper, path, obs_sizes):
    """The exported graph must give the same actions as the PyTorch network."""
    import onnxruntime as ort
    rng = np.random.default_rng(0)
    a = rng.random((32, obs_sizes[0]), dtype=np.float32)
    b = rng.normal(size=(32, obs_sizes[1])).astype(np.float32)
    sess = ort.InferenceSession(path)
    out = sess.run(None, {"obs_0": a, "obs_1": b})
    ref = wrapper(torch.from_numpy(a), torch.from_numpy(b))[2].detach().numpy()
    assert np.allclose(out[2], ref) and np.allclose(
        out[4], ref), "ONNX export mismatch"


if __name__ == "__main__":
    from stable_baselines3 import DQN
    export_onnx(DQN.load(sys.argv[1], device="cpu"), OBS_SIZES, sys.argv[2])
    print("Saved", sys.argv[2])
