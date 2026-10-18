# Edit-mode test scripts

Run with the Unity CLI against the open project (Editor in edit mode, not Play):

    unity command run_script --project-path D:/HarborRacer --file Tools/Tests/<name>.cs --timeout_ms 280000 [--args "[240]"]

Each file has one static class with a `Run(...)` method that returns a text report. Most drive the kart through scripted input with
`Physics.simulationMode = Script`, so they move karts and create objects in the **open scene**.

**Always reload the scene afterwards, without saving:** run `reload2.cs`, or open `ToadHarbor.unity` again. Pressing Play with a
polluted scene gives hundreds of "script missing" warnings and wrong kart positions.

| Script | Checks |
|---|---|
| p2_test, p2wall_test | kart physics numbers, wall scrape (plain wall vs fence vs head-on) |
| p3_test | drift tiers, boost stacking |
| p4_test, p4b_test | zones, off-road, respawn |
| p5_test, p5b_test | glider ramp, anti-gravity |
| p6_test | race flow (countdown, rocket start, laps, results) |
| hazard_test | crates, hit reactions, trolleys, cones |
| item_test | the 30-check item suite |
| ai_stats, ai_stuck, ai_trace, ai_fall, ai_nodes | 8-kart AI simulations (progress, drifts, stalls, falls) |
| panel_test, angle02, ramp_trace | jump panels, angled approaches, overpass ramp |
| respawn_test, agbump, ag_items | respawn + AI re-snap, anti-gravity bump boost, items on tilted surfaces |
| roots, pose_check, floaters2, badshaders, nullmats, peek, where | scene / material / animation diagnostics |
| chain | the standard rebuild chain (item prefabs, markers, kart, race, hazards, item boxes, HUD, look, toads) |
