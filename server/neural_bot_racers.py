#!/usr/bin/env python3
"""
Neural Network Bot Racers — connect to multiplayer server as AI opponents.
Simple MLP-style decision: steer / throttle based on relative waypoint error.
"""

import asyncio
import json
import math
import random
import time
import uuid
from typing import List, Tuple

import websockets

SERVER_URL = "ws://127.0.0.1:8765"
NUM_BOTS = 4
BOT_NAMES = ["NeuralRacer_Alpha", "NeuralRacer_Beta", "NeuralRacer_Gamma", "NeuralRacer_Delta"]

# Simple circular waypoints (same idea as Unity autopilot default)
WAYPOINTS: List[Tuple[float, float]] = [
    (math.cos(i / 16 * math.pi * 2) * 200.0, math.sin(i / 16 * math.pi * 2) * 200.0)
    for i in range(16)
]


class TinyMLP:
    """Minimal 2-layer network: inputs [dx, dz, speed] -> [steer, throttle]"""

    def __init__(self):
        self.w1 = [[random.uniform(-0.5, 0.5) for _ in range(3)] for _ in range(6)]
        self.b1 = [random.uniform(-0.1, 0.1) for _ in range(6)]
        self.w2 = [[random.uniform(-0.5, 0.5) for _ in range(6)] for _ in range(2)]
        self.b2 = [0.0, 0.3]

    @staticmethod
    def tanh(x: float) -> float:
        return math.tanh(x)

    def forward(self, inputs: List[float]) -> List[float]:
        h = []
        for i in range(6):
            s = self.b1[i] + sum(self.w1[i][j] * inputs[j] for j in range(3))
            h.append(self.tanh(s))
        out = []
        for i in range(2):
            s = self.b2[i] + sum(self.w2[i][j] * h[j] for j in range(6))
            out.append(self.tanh(s))
        return out


class BotRacer:
    def __init__(self, name: str):
        self.name = name
        self.net_id = str(uuid.uuid4())
        self.x = random.uniform(-50, 50)
        self.z = random.uniform(-50, 50)
        self.yaw = random.uniform(0, 360)
        self.speed = 0.0
        self.wp_index = 0
        self.mlp = TinyMLP()
        self.rpm = 1000.0
        self.gear = 1

    def step(self, dt: float):
        wx, wz = WAYPOINTS[self.wp_index]
        dx = wx - self.x
        dz = wz - self.z
        dist = math.hypot(dx, dz)

        if dist < 15.0:
            self.wp_index = (self.wp_index + 1) % len(WAYPOINTS)
            wx, wz = WAYPOINTS[self.wp_index]
            dx, dz = wx - self.x, wz - self.z
            dist = math.hypot(dx, dz)

        # Normalize inputs
        inputs = [
            max(-1.0, min(1.0, dx / 100.0)),
            max(-1.0, min(1.0, dz / 100.0)),
            max(0.0, min(1.0, self.speed / 40.0)),
        ]
        steer, throttle = self.mlp.forward(inputs)
        throttle = (throttle + 1.0) * 0.5  # 0..1

        # Integrate simple bicycle model
        self.yaw += steer * 90.0 * dt
        rad = math.radians(self.yaw)
        accel = throttle * 12.0 - self.speed * 0.4
        self.speed = max(0.0, min(45.0, self.speed + accel * dt))
        self.x += math.sin(rad) * self.speed * dt
        self.z += math.cos(rad) * self.speed * dt
        self.rpm = 800.0 + self.speed * 120.0
        self.gear = 1 + int(self.speed / 10.0)

    def state_msg(self) -> dict:
        return {
            "type": "state",
            "pos": {"x": self.x, "y": 0.5, "z": self.z},
            "rot": {"x": 0.0, "y": self.yaw, "z": 0.0, "w": 1.0},
            "originOffset": {"x": 0.0, "y": 0.0, "z": 0.0},
            "rpm": self.rpm,
            "gear": self.gear,
            "steer": 0.0,
        }


async def run_bot(name: str):
    bot = BotRacer(name)
    while True:
        try:
            async with websockets.connect(SERVER_URL) as ws:
                await ws.send(json.dumps({"type": "join", "name": bot.name, "room": "world_1"}))
                print(f"[Bot] {bot.name} joined")

                last = time.time()
                while True:
                    now = time.time()
                    dt = min(0.1, now - last)
                    last = now
                    bot.step(dt)
                    await ws.send(json.dumps(bot.state_msg()))

                    # Non-blocking receive drain
                    try:
                        await asyncio.wait_for(ws.recv(), timeout=0.01)
                    except asyncio.TimeoutError:
                        pass
                    except websockets.exceptions.ConnectionClosed:
                        break

                    await asyncio.sleep(0.05)
        except Exception as e:
            print(f"[Bot] {name} reconnect in 3s: {e}")
            await asyncio.sleep(3)


async def main():
    print(f"Neural Bot Racers — connecting {NUM_BOTS} agents to {SERVER_URL}")
    tasks = [run_bot(BOT_NAMES[i % len(BOT_NAMES)] + f"_{i}") for i in range(NUM_BOTS)]
    await asyncio.gather(*tasks)


if __name__ == "__main__":
    asyncio.run(main())
