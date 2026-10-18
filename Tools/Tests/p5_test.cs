using System.Linq; using System.Reflection; using System.Text; using HarborRacer.Inputs; using HarborRacer.Kart; using HarborRacer.Race; using HarborRacer.Track; using UnityEngine;
public static class P5Test {
 class In : IKartInput { public float Throttle=>1; public float Brake=>0; public float Steer=>0; public float Pitch=>0; public bool DriftHeld=>false; public bool DriftPressed=>false; public bool TrickPressed=>false; public bool ItemPressed=>false; public bool ItemBackPressed=>false; public void EndFixedStep(){} }
 public static string Run(){ Physics.SyncTransforms(); var sb=new StringBuilder();
  var skip=new System.Type[]{typeof(PlayerKartInput),typeof(KartEffects),typeof(KartDebugHUD),typeof(RaceDebugHUD),typeof(HarborRacer.CameraRig.KartCameraRig),typeof(HarborRacer.UI.RaceHud)};
  foreach(var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m=>m is CheckpointTrack?0:m is KartController?2:3)){ if(m==null||skip.Contains(m.GetType())) continue; if(m.GetType().Namespace==null||!m.GetType().Namespace.StartsWith("HarborRacer")) continue; var a=m.GetType().GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance); if(a!=null) a.Invoke(m,null); }
  foreach(var r in Object.FindObjectsByType<RacerProgress>()){ var pk=r.GetComponent<PlayerKartInput>(); if(pk) pk.enabled=false; if(!r.isPlayer){ r.Kart.Body.isKinematic=true; r.transform.position=new Vector3(0,900,0); r.Kart.controlsLocked=true; } }
  var fu=typeof(KartController).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance); Physics.simulationMode=SimulationMode.Script;
  var kart=Object.FindObjectsByType<RacerProgress>().First(r=>r.isPlayer).Kart; var rb=kart.Body;
  var inp=new In(); kart.Input=inp;
  Vector3 d=new Vector3(0.83f,0,0.56f).normalized; Vector3 sp=new Vector3(-96f,-108,280f)-d*75f; if(Physics.Raycast(sp+Vector3.up*6,Vector3.down,out var h,20,HarborRacer.Layers.GroundMask,QueryTriggerInteraction.Ignore)) sp=h.point;
  rb.isKinematic=true; kart.transform.SetPositionAndRotation(sp+Vector3.up*1.0f,Quaternion.LookRotation(d,Vector3.up)); rb.isKinematic=false; rb.linearVelocity=d*85f; rb.rotation=kart.transform.rotation; kart.CurrentSpeed=85; kart.controlsLocked=false; Physics.SyncTransforms();
  bool g=false; float gStart=-1,gEnd=-1,maxBoost=0,peakY=-999,y0=0; int respawn=0; bool trick=false; kart.Glider.GlideTrick+=i=>trick=true; float openT=-1; Vector3 gp=default;
  for(int i=0;i<900;i++){ fu.Invoke(kart,null); Physics.Simulate(0.02f); float t=i*0.02f; var st=kart.State; if(i%25==0&&i<350) sb.AppendLine($"  t={t:F1} pos=({kart.transform.position.x:F0},{kart.transform.position.y:F0},{kart.transform.position.z:F0}) v={kart.ForwardSpeed:F0} grounded={st.Grounded} glide={st.Gliding} boost={kart.Boost.Remaining:F1}");
   if(st.Gliding&&!g){ g=true; gStart=t; gp=kart.transform.position; y0=gp.y; } if(g&&st.Gliding){ peakY=Mathf.Max(peakY,kart.transform.position.y-y0); maxBoost=Mathf.Max(maxBoost,kart.Boost.Remaining); if(kart.Glider.Open&&openT<0) openT=t-gStart; } if(g&&!st.Gliding&&gEnd<0){ gEnd=t; }
   if(kart.Respawn.Respawning){ respawn++; break; } if(gEnd>0&&t>gEnd+3f) break; }
  sb.AppendLine($"glide: started={g} at t={gStart:F1}s pos=({gp.x:F0},{gp.y:F0},{gp.z:F0}) wingsOpenAfter={openT:F2}s trickEvent={trick} peakAbove={peakY:F1}u maxBoost={maxBoost:F2}s duration={(gEnd>0?(gEnd-gStart).ToString("F1"):"still gliding")}s respawn={respawn} end speed={kart.ForwardSpeed:F0} grounded={kart.State.Grounded} gliderOpen={kart.Glider.Open}");
  Physics.simulationMode=SimulationMode.FixedUpdate; return sb.ToString(); } }
