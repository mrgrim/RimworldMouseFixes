# The Problem

The version of Unity used in RimWorld 1.6 has some bugs and quirks in its input event system that causes several issues with UI interactions.

1. Mouse position is only sent with mouse related events breaking things like mouse over UI elements when using mixed inputs.
2. Mouse drag "threshold" is evaluated every mouse poll resulting in stuttery behavior when dragging UI entities.
3. Mouse drag events are sent at the rate of mouse polling causing severe lag.

### Affected Platforms

I only play, develop, and test on Linux. Some or all of these issues may not be present on other operating systems. However, I believe this mod should not cause any ill effects on other platforms. I would appreciate any reports regarding this.

# The Details

The first problem is the one the original version of this mod sought to fix. RimWorld is expecting mouse coordinates to be sent with every event including keyboard events, but Unity only sends them with mouse specific ones. This can cause UI elements that depend on mouse position to not function correctly. One example is the "target highlighter" when hovering over cards for events. If you try to scroll the game world with WASD the target highlighter will stop functioning until you do something with the mouse again.

RimWorld has code that tries to work around this issue when it detects it is running on an affected system, but the workaround is incomplete.

The second issue is caused by a UI feature designed to prevent accidental dragging. When you click and hold on any UI element that can be dragged you must move the mouse a minimum distance before the UI recognizes that you intend to drag it. This is designed to prevent scenarios where in the process of clicking the button you also happen to move the mouse a pixel or two before letting go. You're only supposed to need to do this once and all further movement will smoothly drag the object. However, Unity forces this minimum distance to be exceeded every time the mouse is polled.

What this means is if you move the mouse slowly the object will never be dragged. One obvious place to see this is scroll bar handles. If you click and hold then move the mouse slowly, it will never scroll. The higher your polling rate the faster you have to move the mouse to drag anything.

The final issue results in severe lag when used with high polling rate mice. The problem is Unity sends a "drag" event for every poll of the mouse to the game UI, and the game UI does a lot of work per event. For a 1000Hz mouse at 60 fps this means 16 events per frame. For a 8000Hz mouse this means 133 events per frame.

When the game cannot process the events in the time window of a single frame, this causes even more events to queue up for the next. This can cause a negative feedback loop that can actually crash the game or cause severe lag. In very complex windows this can be quite severe, for example the research tree.

# The Solution

The first issue is straightforward and was the issue the original version of this mod addressed. It simply injects the mouse coordinates into every event as it is fired.

Version 2 of this mod, and why it's been renamed, attempts to address the other two issues. Other mods have tried to address them, but this one takes a novel approach that will hopefully correct the issue universally for the base game and all mod UI's as well as be highly compatible. It adds a function to the Unity Player Loop that overrides the Unity mouse dragging system and events and implements a custom one. This means the game receives only 1 event per frame no matter the polling rate and correctly implements drag thresholds.

# Compatibility

The mod "Mouse Drag Lag Fix (Linux)" by Almantuxas attempts to fix the same high polling rate issue, but it does so through a different strategy. While I don't think having the two installed at the same time would actually break anything, I have decided to mark it as incompatible because they are redundant.