# Tanks

## Tank ML Agent (fixed-map specialist)

This project now includes `TankAgent` at `Assets/Scripts/Tank/TankAgent.cs`.

### What it does
- Uses Unity ML-Agents (`Agent`) for movement, aiming, and charge-shot timing.
- Observes tank state, opponent state, line of sight, health, distance, and shot charge.
- Uses reward shaping for:
	- winning rounds,
	- dealing damage,
	- minimizing received damage,
	- improving distance/alignment to target.
- Optional inference aim assist (enabled by default in the script) for stronger play.

### Scene / Prefab setup
1. Open your tank prefab (the one used as AI player in `GameManager`).
2. Add `Behavior Parameters` component:
	 - Behavior Name: `TankAgent`
	 - Space Size:
		 - Continuous Actions: `3`
		 - Discrete Branches: `0`
3. Add `Decision Requester` component (Decision Period recommended: `2` to `5`).
4. Add `TankAgent` component.
5. Ensure this prefab already has:
	 - `TankMovement`, `TankShooting`, `TankHealth`, `Rigidbody`.
6. In your player setup UI, mark this player as computer-controlled.

`TankManager` will now automatically prefer `TankAgent` over the scripted `TankAI` when both are possible.

### Training
Config file: `ml-agents/tank_agent.yaml`

Example command (from project root):

```bash
mlagents-learn ml-agents/tank_agent.yaml --run-id=tank_fixed_map_v1
```

Then press Play in Unity.

To continue training:

```bash
mlagents-learn ml-agents/tank_agent.yaml --run-id=tank_fixed_map_v1 --resume
```

### Important note
No ML policy can be mathematically guaranteed to beat every human every time. For a specific map and fixed ruleset, this setup is designed to train a highly dominant policy with self-play and strong reward shaping.
