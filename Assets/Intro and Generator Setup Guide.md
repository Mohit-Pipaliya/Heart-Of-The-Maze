# Intro Lift and Generator Wheel Setup

## Intro lift sequence

1. Put `IntroSequenceManager.cs`, `LiftBox.cs`, `IntroCameraController.cs`, and `IntroPromptUI.cs` under `Assets/Scripts`. Keep `IntroSequenceSetupWindow.cs` under `Assets/Editor`.
2. Import Mixamo animations as **FBX for Unity**, **Without Skin**, with the Rig type set to **Humanoid**. Use a non-looping **Getting Up** clip and a looping **Idle** clip. An optional looping lying/sleeping clip can play before Getting Up.
3. In Unity, open **Tools > Intro Sequence > Auto Setup**. Drag the player root and the lift box root into the first two fields, then assign the Getting Up, Idle, and optional Lying FBX/clip assets. Set the rise distance and duration, then click **Setup** and save the scene.
4. The tool creates `Assets/Animations/IntroAnimator.controller`, the eye camera point, and a box target point. Keep the player positioned on the box floor before play. Ensure the lift box root contains the geometry that should rise; keep the target point clear of the top exit.
5. The prompt canvas and EventSystem are created at runtime. The player wakes automatically, presses **U** to raise the box, then receives control when the box stops. If using Cinemachine, the intro controller disables the brain during the eye view and re-enables it after blending back; check the active virtual camera priority if another camera takes over.

The setup window can be run again safely: it reuses the eye and target points and the existing sequence components, and regenerates the controller asset. It prints warnings for a non-Humanoid FBX or when the imported clip has Root Transform Position (Y) Bake Into Pose disabled.

## Generator light wheel puzzle

`GeneratorLightPuzzle.cs` is the runtime interaction and power source. Add it to an object with a trigger collider at the generator and assign these references in its Inspector:

- **Day Night Controller**: the scene's `DayNightController` (the script can find one automatically).
- **Wheel Stand Point**: an empty transform where the player should walk and stop.
- **Wheel Stand Beacon**: optional particle effect. Leave it empty to have the script create a pulsing blue beacon at the stand point at night.
- **Camera Stand Point**: an empty transform for the camera view while using the wheel.
- **Wheel Pivot**: the rotating wheel transform. Its local rotation axis defaults to Z; change it to match the wheel.
- **Wheel Marker** and **Target Wheel Marker**: child transforms that show the current handle direction and the alignment goal. Put them at the same radius from the pivot.
- **House Light Triggers**: every house's `HouseLightTrigger` component. This connects each house to this generator's solved/night power state.

The generator trigger must have **Is Trigger** enabled. At night, a blue particle beacon marks the Wheel Stand Point. Walk there manually; when the player is within **Wheel Interaction Radius**, the player locks in place and the wheel camera/puzzle starts automatically. Press **A** or **D** to rotate the wheel until the marker aligns. No L key or NavMesh route is used for this interaction. Solving it makes generator power available at night. The puzzle checks `DayNightController.IsNight`, so set its cycle time to at least 0.5 to try it immediately in Play mode. The camera uses the existing `CameraController` stand mode.

### Campfire power

The scene campfire's `RealisticFire` must have **Requires Generator Power** enabled and its **Generator Power Source** assigned to this `GeneratorLightPuzzle`. The scene is wired this way: the campfire remains extinguished until the wheel puzzle is solved and it is night, then it ignites automatically. It turns off again when the generator is unavailable (including daytime).

### Per-house trigger lights

For each house, create a trigger volume and add `HouseLightTrigger`:

1. Create an empty object inside/around the house and add a **Box Collider**. Enable **Is Trigger** and resize it to cover the area where the player should activate the house lights. `HouseLightTrigger` automatically adds a kinematic Rigidbody if needed for trigger callbacks.
2. Add the `HouseLightTrigger` component. Assign all the house's `Light` components to **House Lights**. Leave **Generator** empty if there is only one generator puzzle in the scene; it will be found automatically. With multiple generators, assign the one that powers this house.
3. On the generator's `GeneratorLightPuzzle`, add this house's `HouseLightTrigger` to **House Light Triggers**. This explicitly connects the house to the generator. Repeat for each house.
4. Disable the house lights initially if they should start off. At runtime they are on only when the puzzle is solved, it is night, and the player is inside that house's trigger. Leaving the volume turns them off. Daytime turns them off even if the player remains inside; they can turn on again at night if the player is still there.

The trigger identifies the player through `PlayerController`, so the player GameObject (or its collider parent) must have that component. Give each house its own trigger volume and light list. These per-house lights are controlled by `HouseLightTrigger`; they should not also be assigned as a global always-on light list on the generator.

## Solar panel sun tracking

`SolarPanelDayAnimation` now keeps the panel's surface normal aimed toward the `DayNightController` sun during daytime, while it continues the assigned Animator during the day and pauses it at night.

Use this hierarchy so the Animator cannot overwrite the tracking rotation:

```text
SolarPanelTrackingPivot   <- SolarPanelDayAnimation; rotates toward the sun
└── SolarPanelAnimated    <- Animator and its existing animation/controller
```

Assign the scene `DayNightController`, the child panel's Animator, and the parent tracking pivot (or leave it empty to use the `SolarPanel` root). Set **Local Panel Normal** to whichever local axis points out from the panel's active surface (usually Up or Forward). Leave **Track Sun Elevation** on to follow both height and direction; turn it off if the panel should only turn horizontally. **Tracking Speed** controls how quickly it follows. The DayNightController must have its Sun instance set up; its generated/exposed scene sun transform is used automatically.

## Meeting seat interaction

On the bench's `MeetingSeatSystem`, set **Seat Arrival Point** on the clear floor just in front of the seat and **Seat Face Target** toward the table. The player sees a warm particle beacon and walks to that point manually. Assign the generator to **Generator Power Source** (the scene already does this); the **Press C to Join Meeting** UI stays hidden until generator power is available, and then appears only within **Seat Prompt Radius** (default 1.5 m). Pressing C starts the existing sit-down, seated-idle, and stand-up animations. The old MeetingTrigger volume is removed; no auto-walk or NavMesh bake is needed for this interaction. Leave **Seat Arrival Beacon** empty to have the script create it at runtime, or assign a custom particle effect.
