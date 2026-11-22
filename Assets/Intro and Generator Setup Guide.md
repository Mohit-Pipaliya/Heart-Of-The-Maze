# Intro Lift and Generator Wheel Setup

## Intro lift sequence

1. Put `IntroSequenceManager.cs`, `LiftBox.cs`, `IntroCameraController.cs`, and `IntroPromptUI.cs` under `Assets/Scripts`. Keep `IntroSequenceSetupWindow.cs` under `Assets/Editor`.
2. Import Mixamo animations as **FBX for Unity**, **Without Skin**, with the Rig type set to **Humanoid**. Use a non-looping **Getting Up** clip and a looping **Idle** clip. An optional looping lying/sleeping clip can play before Getting Up.
3. In Unity, open **Tools > Intro Sequence > Auto Setup**. Drag the player root and the lift box root into the first two fields, then assign the Getting Up, Idle, and optional Lying FBX/clip assets. Set the rise distance and duration, then click **Setup** and save the scene.
4. The tool creates `Assets/Animations/IntroAnimator.controller`, the eye camera point, and a box target point. Keep the player positioned on the box floor before play. Ensure the lift box root contains the geometry that should rise; keep the target point clear of the top exit.
5. The prompt canvas and EventSystem are created at runtime. The player wakes automatically, presses **U** to raise the box, then receives control when the box stops. If using Cinemachine, the intro controller disables the brain during the eye view and re-enables it after blending back; check the active virtual camera priority if another camera takes over.

The setup window can be run again safely: it reuses the eye and target points and the existing sequence components, and regenerates the controller asset. It prints warnings for a non-Humanoid FBX or when the imported clip has Root Transform Position (Y) Bake Into Pose disabled.

## Generator light wheel puzzle

`GeneratorLightPuzzle.cs` is the runtime interaction. Add it to an object with a trigger collider at the generator and assign these references in its Inspector:

- **Day Night Controller**: the scene's `DayNightController` (the script can find one automatically).
- **Wheel Stand Point**: an empty transform where the player should walk and stop.
- **Camera Stand Point**: an empty transform for the camera view while using the wheel.
- **Wheel Pivot**: the rotating wheel transform. Its local rotation axis defaults to Z; change it to match the wheel.
- **Wheel Marker** and **Target Wheel Marker**: child transforms that show the current handle direction and the alignment goal. Put them at the same radius from the pivot.
- **Powered Lights**: every `Light` that should turn on after alignment.

The trigger must have **Is Trigger** enabled, and the scene needs a baked NavMesh / clear walking route from the player to the stand point. During night, enter the trigger and press **L** to walk to the wheel. Press **A** or **D** to rotate it until the marker aligns; the assigned lights then turn on. The puzzle checks `DayNightController.IsNight`, so set its cycle time to at least 0.5 to try it immediately in Play mode. The camera uses the existing `CameraController` stand mode.

The generator script does not create the wheel mesh or decide which room lights belong to the puzzle; make the pivot/markers and assign the desired lights in the Inspector. Keep those lights disabled initially if they should only be powered by solving the puzzle.

## Meeting seat fix

On the bench's `MeetingSeatSystem`, set **Seat Arrival Point** just in front of the seat and **Seat Face Target** toward the table. The approach now keeps a stable avoidance direction, refreshes its NavMesh route when blocked, and performs a final exact placement before starting the sit animation. Adjust `walkToSeatSpeed` and `stoppingDistance` per bench scale.
