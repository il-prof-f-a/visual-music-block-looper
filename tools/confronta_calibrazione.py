"""Compare boundary-based rectification models without modifying the saved CAL3 file.

Uses NumPy only. Synthetic interior error is explicitly NOT a measurement of the
real table: a new raw frame is needed to evaluate its projected straight lines.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np


METHODS = ("omografia", "tps", "coons", "polinomio3")


def project(h, points):
    q = np.c_[points, np.ones(len(points))] @ h.T
    return q[:, :2] / q[:, 2:3]


def homography(camera, table):
    def normalize(p):
        center = p.mean(axis=0)
        scale = np.sqrt(2) / np.linalg.norm(p - center, axis=1).mean()
        t = np.array([[scale, 0, -scale * center[0]],
                      [0, scale, -scale * center[1]], [0, 0, 1]])
        return project(t, p), t
    c, tc = normalize(camera)
    t, tt = normalize(table)
    rows = []
    for (x, y), (u, v) in zip(c, t):
        rows.extend(([-x, -y, -1, 0, 0, 0, u*x, u*y, u],
                     [0, 0, 0, -x, -y, -1, v*x, v*y, v]))
    _, _, vh = np.linalg.svd(np.array(rows))
    h = np.linalg.inv(tt) @ vh[-1].reshape(3, 3) @ tc
    return h / h[2, 2]


def terms(p):
    x, y = p.T
    return np.c_[np.ones(len(p)), x, y, x*x, x*y, y*y,
                 x*x*x, x*x*y, x*y*y, y*y*y]


def kernel(p, q):
    r = ((p[:, None, :] - q[None, :, :])**2).sum(axis=2)
    return .5*r*np.log(np.maximum(r, 1e-24))


class Model:
    def __init__(self, controls, observed, method):
        self.controls, self.method = controls, method
        self.residual = observed - controls
        self.lo, self.hi = controls.min(axis=0), controls.max(axis=0)
        if method == "tps":
            a = np.c_[np.ones(len(controls)), controls]
            system = np.block([[kernel(controls, controls), a],
                               [a.T, np.zeros((3, 3))]])
            self.coefficients = np.linalg.solve(system, np.r_[self.residual, np.zeros((3, 2))])
        elif method == "polinomio3":
            self.coefficients, _, self.rank, _ = np.linalg.lstsq(terms(controls), self.residual, rcond=None)

    def edge(self, p, indices, axis):
        nodes = self.controls[indices, axis]
        weights = np.ones((len(p), 4))
        for i in range(4):
            for j in range(4):
                if i != j:
                    weights[:, i] *= (p[:, axis] - nodes[j]) / (nodes[i] - nodes[j])
        return weights @ self.residual[indices]

    def apply(self, p):
        if self.method == "omografia":
            return p.copy()
        if self.method == "tps":
            a = np.c_[kernel(p, self.controls), np.ones(len(p)), p]
            return p + a @ self.coefficients
        if self.method == "polinomio3":
            return p + terms(p) @ self.coefficients
        u, v = ((p - self.lo) / (self.hi - self.lo)).T
        corners = self.residual[[0, 3, 9, 6]]
        bilinear = (np.c_[(1-u)*(1-v), u*(1-v), (1-u)*v, u*v] @ corners)
        return (p + (1-v[:, None])*self.edge(p, [0, 1, 2, 3], 0)
                + v[:, None]*self.edge(p, [9, 8, 7, 6], 0)
                + (1-u[:, None])*self.edge(p, [0, 11, 10, 9], 1)
                + u[:, None]*self.edge(p, [3, 4, 5, 6], 1) - bilinear)


def grid(low=.05, high=.95, n=31):
    return np.array([(x, y) for y in np.linspace(low, high, n)
                     for x in np.linspace(low, high, n)])


def geometry(model):
    p = grid(0, 1, 41)
    e = 1e-5
    dx = (model.apply(p + [e, 0]) - model.apply(p - [e, 0])) / (2*e)
    dy = (model.apply(p + [0, e]) - model.apply(p - [0, e])) / (2*e)
    j = np.stack((dx, dy), axis=2)
    det = np.linalg.det(j)
    sv = np.linalg.svd(j, compute_uv=False)
    return {"jacobiano_min": float(det.min()), "jacobiano_max": float(det.max()),
            "anisotropia_max": float((sv[:, 0]/sv[:, 1]).max()),
            "nessuna_piega_campionata": bool((det > .05).all())}


def synthetic_camera(p, coefficient):
    xy = p - .5
    uv = .5 + xy*(1 + coefficient*(xy*xy).sum(axis=1, keepdims=True))
    u, v = uv.T
    w = 1 + .12*u + .06*v
    return np.c_[(100 + 1000*u + 80*v)/w, (60 + 25*u + 650*v)/w]


def compare(source):
    lines = source.read_text(encoding="utf-8-sig").splitlines()
    if lines[0] != "VMBL-CAL3" or len(lines) != 15:
        raise ValueError("Required CAL3 with the twelve original references")
    h = np.array([float(v) for v in lines[1].split(";")]).reshape(3, 3)
    rows = np.array([[float(v) for v in line.split(";")] for line in lines[3:]])
    camera, controls = rows[:, :2], rows[:, 2:]
    observed = project(h, camera)
    inv = np.linalg.inv(h)
    report = {"origine_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
              "limite": "No raw frame acquired: real interior straightness is not measured.",
              "riferimenti_reali": {}, "prove_sintetiche": {}}
    for method in METHODS:
        model = Model(controls, observed, method)
        errors = np.linalg.norm(project(inv, model.apply(controls)) - camera, axis=1)
        report["riferimenti_reali"][method] = dict(geometry(model),
            rms_riferimenti_px=float(np.sqrt((errors**2).mean())),
            max_riferimenti_px=float(errors.max()))
    inside = grid(.1, .9)
    for k in (-.15, -.3, -.45):
        cam = synthetic_camera(controls, k)
        hs = homography(cam, controls)
        obs = project(hs, cam)
        truth = synthetic_camera(inside, k)
        results = {}
        for method in METHODS:
            pred = project(np.linalg.inv(hs), Model(controls, obs, method).apply(inside))
            error = np.linalg.norm(pred - truth, axis=1)
            results[method] = {"rms_interno_px": float(np.sqrt((error**2).mean())),
                               "max_interno_px": float(error.max())}
        report["prove_sintetiche"][str(k)] = results
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("calibrazione", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = compare(args.calibrazione)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
