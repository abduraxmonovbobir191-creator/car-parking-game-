#!/usr/bin/env python3
"""MODULE 33 — Procedural High-Res PBR Texture Generator"""
import os, math, random
OUT = os.path.join(os.path.dirname(__file__), "..", "client", "Assets", "Textures", "PBR")
os.makedirs(OUT, exist_ok=True)

def write_ppm(path, w, h, pixels):
    with open(path, "w") as f:
        f.write(f"P3\n{w} {h}\n255\n")
        for y in range(h):
            for x in range(w):
                r,g,b = pixels[y*w+x]
                f.write(f"{r} {g} {b} ")
            f.write("\n")
    print(f"[M33] {path}")

def asphalt(w=256, h=256):
    px = []
    for y in range(h):
        for x in range(w):
            n = (math.sin(x*0.3)+math.sin(y*0.25)+random.random()*0.3)*40 + 40
            px.append((int(n), int(n), int(n*0.9)))
    return px

def carbon(w=256, h=256):
    px = []
    for y in range(h):
        for x in range(w):
            weave = ((x//8 + y//8) % 2) * 30
            px.append((20+weave, 20+weave, 22+weave))
    return px

def skin(w=128, h=128):
    px = []
    for y in range(h):
        for x in range(w):
            base = 180 + random.randint(-15,15)
            px.append((base, base-20, base-30))
    return px

def main():
    write_ppm(os.path.join(OUT, "asphalt_albedo.ppm"), 256, 256, asphalt())
    write_ppm(os.path.join(OUT, "carbon_albedo.ppm"), 256, 256, carbon())
    write_ppm(os.path.join(OUT, "skin_albedo.ppm"), 128, 128, skin())
    print("[MODULE 33] PBR albedo maps written (PPM).")

if __name__ == "__main__":
    main()
