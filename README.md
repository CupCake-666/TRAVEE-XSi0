# TRAVEE – Tic-Tac-Toe for Post-Stroke Rehabilitation

A Unity game that turns upper-limb rehabilitation exercises into a match of tic-tac-toe against a friendly AI.
It is the first of three rehabilitation games built for my master's dissertation within **TRAVEE**, a virtual
reality system for the recovery of patients with hemiparesis after a stroke.

The patient plays against an animated 3D character. Every move means reaching towards the board with the
affected arm, and the AI's difficulty is tuned so that the patient keeps winning and keeps practising.

> **Status:** playable in 3D on desktop (the mouse stands in for the patient's hand). The VR version
> (head tracking and touching cells with the hand) is the next step; the code is already structured for it.

## The problem it addresses

Rehabilitation after a stroke depends on many repetitions of the same movements, which quickly becomes
tedious and frustrating. A game the patient already knows keeps them engaged, but a standard tic-tac-toe AI
either always wins or always draws, which is discouraging. This project gives the therapist an opponent whose
behaviour can be tuned to the patient: from one that cannot lose to them, to a fair one that rewards real strategy.

## Features

- **AI with four difficulty levels**

  | Level | AI behaviour | Patient starts |
  |---|---|---|
  | 1 – Very easy | Helps the patient win; never blocks or completes its own line. The patient cannot lose. | 100% |
  | 2 – Easy | Looks like it defends (blocks when it can afford to), but the patient still cannot lose. | 75% |
  | 3 – Medium | Plays optimally with a constant 45% mistake rate; a typical patient wins about 67% of games. | 60% |
  | 4 – Challenge | Plays perfectly and sets traps; at most one subtle mistake per game, punishable by a single precise reply. | 50% |

- **Weighted coin flip** decides who starts (the starter plays X); the odds depend on the level.
- **Animated 3D opponent**: waits beside the patient, walks behind the board on its turn (the board turns
  see-through), and the cell it is about to pick glows red. It then touches that exact cell without passing through
  the board: the standing position is computed from the model's arm length, the hand is driven by inverse
  kinematics for the whole motion, a plane constraint keeps every hand bone behind the board, and a small
  feedback loop brings the fingertip onto the cell. It applauds when the patient wins.
- **Three environments**, chosen from the menu: a forest clearing, a mountain plateau at night with stars and
  a campfire, and a beach with a moving sea. The props are stylized CC0 models, the skies are CC0 360° photos,
  and the mountains, islands and sea are generated in code. Each environment is built all around the
  patient (a pond with water lilies, a campfire with a flickering flame, fireflies at night, the island's
  green interior), because in VR the patient can turn a full 360°.
- **Board placed for a seated patient**: by default its centre is at eye level (1.2 m), so the patient looks
  straight ahead. The character is scaled automatically so that it can reach the top row. Height and distance
  are adjustable in the Inspector; a lower board keeps every cell below shoulder height.
- **Sound**: ambience for each environment (birds, crickets, waves) with seamless cross-faded looping, feedback
  for every action (buttons, hovering a cell, placing a piece, coin flip, result), applause when the patient
  wins, and the character's footsteps, played from its position.
- **Gaze response**: when the patient looks at the character during their turn, it waves.
- **Affected-side awareness**: the therapist selects the affected hand. The character stands on that side by
  default, encouraging the patient to turn towards it, and on the easy levels the AI leaves the cells on the
  affected side free for the patient.
- **Session logging for analysis**: two CSV files, one row per game (level, result, duration) and one row per
  move (cell, whether it is on the affected side, reaction time in milliseconds).

## How it works

The code is split into three layers, so the presentation can change (2D → 3D → VR) without touching the game logic:

| Layer | Folder | Contents |
|---|---|---|
| Game logic | `Assets/Scripts/Core` | Board, rules, match state. Plain C#, no Unity dependency. |
| AI | `Assets/Scripts/Core/AI` | Minimax with memoization (levels 3–4) and expectimax (levels 1–2), written from scratch. |
| Unity presentation | `Assets/Scripts/Game` | Game flow, environments, board, animated character, UI, sound, CSV logging. |
| Editor tools | `Assets/Scripts/Editor` | One-click scene setup and automatic import of Mixamo characters and animations. |
| Tests | `Assets/Tests/EditMode` | NUnit tests that simulate thousands of games per level to check the AI's guarantees. |

The game flow only talks to three interfaces: `ICellInputSource` (a cell was selected), `IBoardView` (show the
board) and `IOpponentPresenter` (how the AI looks while it plays). Moving to VR means adding a new input source
for hand touches; the logic, AI, character and logging stay the same.

Detailed, line-by-line explanations of the code (in Romanian) are in [`Docs/Explicatii`](Docs/Explicatii).
How to rearrange or extend the environments by hand in the editor is described in
[`Docs/Cum_modific_scena.md`](Docs/Cum_modific_scena.md), and the history of changes in
[`Docs/Jurnal_schimbari.md`](Docs/Jurnal_schimbari.md).

## Tech stack

- Unity 6.3 LTS (6000.3.27f2), C#
- Universal Render Pipeline 17.3, Input System 1.20, uGUI
- Mecanim (Humanoid rig, inverse kinematics)
- Unity Test Framework (NUnit)
- Character and animations from [Mixamo](https://www.mixamo.com)
- Environment models, skies and sounds: CC0 assets by Quaternius, Kenney, Poly Haven, BigSoundBank and OpenGameArt
  contributors (see [CREDITS.md](CREDITS.md))

## Running the project

1. Open the project in **Unity 6000.3.27f2** or newer.
2. The Mixamo character and animations are **not included** in this repository, because Adobe's terms do not
   allow redistributing them as raw files. Download them for free from [mixamo.com](https://www.mixamo.com)
   as **FBX for Unity**:
   - the character, with skin, saved as `Assets/Art/Character/Ch19_nonPBR.fbx` (the character used here is
     Mixamo's "Ch19"; any humanoid works if you update `ModelPath` in `CharacterImportSettings.cs`);
   - `Happy Idle` (with skin) in `Assets/Art/Character/`;
   - `Happy Walk`, `Waving`, `Thinking`, `Look Around`, `Button Pushing`, `Pointing`, `Reaching Out`,
     `Clapping`, `Victory Idle` and `Shrugging` (without skin, *In Place* where available) in
     `Assets/Art/Character/Animations/`.

   The editor scripts configure everything automatically on import (Humanoid rig, avatar, looping clips,
   textures).
3. Run **TRAVEE → Creează scena X și 0 (3D)** from the Unity menu bar, then press **Play**.

Without the character the game still runs; the AI simply has no body.

Controls: left click selects a cell, hold right click and move the mouse to look around (a full 360°), Space looks forward again.
The **Peisaj** button in the menu switches the environment; the choice is remembered between runs.

After adding a model to `Assets/Art/Nature`, a sky panorama to `Assets/Art/Sky` or a sound to `Assets/Audio`, run the TRAVEE menu command again:
it rebuilds the asset library (`Assets/Art/GameAssets.asset`) that the game loads them from.

Session data is written to `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\TRAVEE\XSi0\`
(`games.csv` and `moves.csv`).

## Testing

Open **Window → General → Test Runner → EditMode → Run All**. The tests check, among other things, that the AI
never wins on levels 1–2 against random, beginner and perfect simulated players, and that level 4 is
unbeatable when its mistake rate is set to zero.

## License

Code released under the [MIT License](LICENSE). The environment models, sky panoramas and sounds in this repository are CC0
(public domain); their sources are listed in [CREDITS.md](CREDITS.md). The Mixamo character and animations are
subject to Adobe's terms and are not part of this repository.
