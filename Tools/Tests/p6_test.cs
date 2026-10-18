using System.Linq; using System.Reflection; using System.Text; using HarborRacer.Inputs; using HarborRacer.Kart; using HarborRacer.Race; using HarborRacer.Track; using UnityEngine;
public static class P6Test {
 class In : IKartInput { public float thr; public float Throttle=>thr; public float Brake=>0; public float Steer=>0; public float Pitch=>0; public bool DriftHeld=>false; public bool DriftPressed=>false; public bool TrickPressed=>false; public bool ItemPressed=>false; public bool ItemBackPressed=>false; public void EndFixedStep(){} }
 public static string Run(){ Physics.SyncTransforms(); var sb=new StringBuilder();
  var skip=new System.Type[]{typeof(PlayerKartInput),typeof(KartEffects),typeof(KartDebugHUD),typeof(RaceDebugHUD),typeof(HarborRacer.CameraRig.KartCameraRig),typeof(HarborRacer.UI.RaceHud)};
  foreach(var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m=>m is CheckpointTrack?0:m is KartController?2:m is RaceManager?5:3)){ if(m==null||skip.Contains(m.GetType())) continue; if(m.GetType().Namespace==null||!m.GetType().Namespace.StartsWith("HarborRacer")) continue; var a=m.GetType().GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance); if(a!=null) a.Invoke(m,null); }
  foreach(var r in Object.FindObjectsByType<RacerProgress>()){ var pk=r.GetComponent<PlayerKartInput>(); if(pk) pk.enabled=false; var pd=r.GetComponent<PathDriverInput>(); if(pd) pd.enabled=false; }
  var rm=Object.FindFirstObjectByType<RaceManager>(); var player=null as RacerProgress;
  var st=new System.Collections.Generic.List<string>(); rm.StateChanged+=s=>st.Add($"{s}@{rm.RaceTime:F2}"); var ticks=new System.Collections.Generic.List<string>(); float clock=0f; rm.CountdownTick+=v=>ticks.Add($"{v}@{clock:F2}");
  rm.BeginRace(); player=rm.Player; var inp=new In(); player.Kart.Input=inp; var rs=player.GetComponent<RocketStart>();
  // 1+2+3: state timeline, lock, rocket start (hold only the last 1.5 s of the countdown)
  bool lockedBeforeGo=true; bool unlockedAtGo=false; float boostAtGo=0; int steps=0;
  while(rm.State!=RaceState.Racing&&steps<2000){ clock+=0.02f; steps++; bool counting=rm.State==RaceState.Countdown; inp.thr=(counting&&clock>=4.5f)?1f:0f; rs.Tick(0.02f); rm.Tick(0.02f); if(rm.State!=RaceState.Racing&&!player.Kart.controlsLocked) lockedBeforeGo=false; }
  unlockedAtGo=!player.Kart.controlsLocked; boostAtGo=player.Kart.Boost.Remaining;
  sb.AppendLine("1 states: "+string.Join(" -> ",st)+" | countdown ticks: "+string.Join(", ",ticks)+" | locked until GO="+lockedBeforeGo+" unlockedAtGo="+unlockedAtGo);
  sb.AppendLine($"3 rocket start (held the last 1.5 s): boost at GO = {boostAtGo:F2} s (spec 1.0)");
  // 4: laps
  var racers=rm.Racers; var track=CheckpointTrack.Current; int n=track.Count; var ai=racers.First(r=>!r.isPlayer);
  ai.Init(3); for(int i=0;i<n;i++) ai.OnCheckpointEntered(i); ai.OnFinishLine(); int lapAfterFull=ai.Lap;
  var ai2=racers.Where(r=>!r.isPlayer).ElementAt(1); ai2.Init(3); for(int i=0;i<5;i++) ai2.OnCheckpointEntered(i); for(int i=12;i<n;i++) ai2.OnCheckpointEntered(i); ai2.OnFinishLine(); int lapAfterSkip=ai2.Lap; int passedSkip=ai2.CheckpointsPassed;
  var ai3=racers.Where(r=>!r.isPlayer).ElementAt(2); ai3.Init(3); ai3.OnFinishLine(); int lapAfterLineOnly=ai3.Lap;
  sb.AppendLine($"4 laps: all {n} checkpoints then the line -> lap {lapAfterFull} (expects 2); skipped checkpoints 5-11 then the line -> lap {lapAfterSkip}, passed {passedSkip} (expects 1, 5); line without checkpoints -> lap {lapAfterLineOnly} (expects 1)");
  // 5: positions
  foreach(var r in racers) r.Init(3); var order=racers.ToList(); for(int i=0;i<order.Count;i++) for(int k=0;k<(order.Count-i)*3;k++) order[i].OnCheckpointEntered(k%n==order[i].NextCheckpoint?k%n:order[i].NextCheckpoint);
  rm.SortRacers(); sb.AppendLine("5 live order after giving racer 0 the most checkpoints: "+string.Join(", ",rm.Sorted.Select(r=>r.racerName+"("+r.CheckpointsPassed+")")));
  // 6: finish flow: player completes 3 laps
  player.Init(3); bool finalLap=false; rm.FinalLapStarted+=()=>finalLap=true; var fin=new System.Collections.Generic.List<string>(); rm.RacerFinished+=r=>fin.Add(r.racerName);
  for(int lap=0;lap<3;lap++){ for(int i=0;i<n;i++) player.OnCheckpointEntered(i); player.OnFinishLine(); rm.Tick(0.02f); }
  sb.AppendLine($"6 player finished={player.Finished} finalLapEvent={finalLap} state={rm.State} finishedEvents={string.Join(",",fin)} autopilot={(player.Kart.Input is PathDriverInput)}");
  float t=0; while(rm.State!=RaceState.Results&&t<40f){ rm.Tick(0.02f); t+=0.02f; }
  sb.AppendLine($"6 results reached after {t:F1} s with others unfinished (spec: all finish or 20 s): state={rm.State} rows={rm.Results.Count} first={(rm.Results.Count>0?rm.Results[0].Name:"-")}");
  return sb.ToString(); } }
