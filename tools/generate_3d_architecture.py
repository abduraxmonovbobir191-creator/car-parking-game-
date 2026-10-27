#!/usr/bin/env python3
"""
MODULE 32 — Procedural 3D City Buildings & Interiors .OBJ Generator
Generates multi-room architecture meshes: penthouse, dealership, garage, bridge, interchange.
"""
import math, os
from dataclasses import dataclass, field
from typing import List, Tuple

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "Models", "Architecture")
os.makedirs(OUTPUT_DIR, exist_ok=True)

Vector3 = Tuple[float, float, float]

@dataclass
class ArchMesh:
    name: str
    vertices: List[Vector3] = field(default_factory=list)
    faces: List[Tuple[int,int,int]] = field(default_factory=list)
    def v(self, p): 
        self.vertices.append(p)
        return len(self.vertices)
    def f(self, a,b,c):
        self.faces.append((a,b,c))
    def box(self, mn, mx):
        x0,y0,z0 = mn; x1,y1,z1 = mx
        b = len(self.vertices)
        pts = [(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)]
        for p in pts: self.v(p)
        for a,bb,c in [(0,1,2),(0,2,3),(4,6,5),(4,7,6),(0,4,5),(0,5,1),(2,6,7),(2,7,3),(0,3,7),(0,7,4),(1,5,6),(1,6,2)]:
            self.f(b+a+1, b+bb+1, b+c+1)
    def write(self, path):
        with open(path,"w") as f:
            f.write(f"# MODULE 32 {self.name}\no {self.name}\n")
            for v in self.vertices: f.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
            for a,b,c in self.faces: f.write(f"f {a} {b} {c}\n")
        print(f"[M32] {path} verts={len(self.vertices)} faces={len(self.faces)}")

def penthouse():
    m = ArchMesh("luxury_oceanfront_penthouse")
    m.box((-18,0,-12),(18,3.2,12))
    m.box((-14,3.2,-8),(14,6.5,8))
    m.box((-6,6.5,-4),(6,8.0,4))
    m.box((-2,8.0,-1),(2,10.5,1))
    return m

def dealership():
    m = ArchMesh("flagship_car_dealership_showroom")
    m.box((-30,0,-20),(30,8,20))
    m.box((-28,0,-18),(28,0.15,18))
    m.box((-25,4,-15),(25,7.5,15))
    m.box((-8,0,18),(-2,6,22))
    return m

def garage():
    m = ArchMesh("underground_tuning_garage")
    m.box((-16,-4,-12),(16,0.5,12))
    m.box((-14,-3.5,-10),(14,-0.2,10))
    m.box((-12,-0.2,-8),(12,3.5,8))
    m.box((-4,-0.2,8),(4,4,12))
    return m

def bridge():
    m = ArchMesh("suspension_bridge_span")
    for i in range(40):
        z = -80 + i*4
        m.box((-6,8,z),(-4,10,z+3.8))
        m.box((4,8,z),(6,10,z+3.8))
        m.box((-5,9.5,z),(5,10,z+3.8))
    m.box((-8,0,-85),(8,12,-75))
    m.box((-8,0,75),(8,12,85))
    return m

def interchange():
    m = ArchMesh("highway_cloverleaf_interchange")
    m.box((-80,0,-6),(80,1,6))
    m.box((-6,0,-80),(6,1,80))
    for ang, ox, oz in [(45,40,40), (-45,-40,40), (135,40,-40), (-135,-40,-40)]:
        m.box((ox-4,1,oz-4),(ox+4,3,oz+4))
    return m

def main():
    for name, fn in [
        ("luxury_oceanfront_penthouse", penthouse),
        ("flagship_car_dealership_showroom", dealership),
        ("underground_tuning_garage", garage),
        ("suspension_bridge_span", bridge),
        ("highway_cloverleaf_interchange", interchange),
    ]:
        mesh = fn()
        mesh.write(os.path.join(OUTPUT_DIR, f"{name}.obj"))
    print("[MODULE 32] Architecture meshes complete.")

if __name__ == "__main__":
    main()
