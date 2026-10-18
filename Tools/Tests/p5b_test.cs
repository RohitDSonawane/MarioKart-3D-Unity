using System.Linq; using System.Reflection; using System.Text; using HarborRacer.Inputs; using HarborRacer.Kart; using HarborRacer.Race; using HarborRacer.Track; using UnityEngine;
public static class P5bTest {
 class In : IKartInput { public float Throttle=>1; public float Brake=>0; public float Steer=>0; public float Pitch=>0; public bool DriftHeld=>false; public bool DriftPressed=>false; public bool TrickPressed=>false; public bool ItemPressed=>false; public bool ItemBackPressed=>false; public void EndFixedStep(){} }
 public static string Run(){ Physics.SyncTransforms(); var sb=new StringBuilder();
  var skip=new System.Type[]{typeof(PlayerKartInput),typeof(KartEffects),typeof(KartDebugHUD),typeof(RaceDebugHUD),typeof(HarborRacer.CameraRig.KartCameraRig),typeof(HarborRacer.UI.RaceHud)};
  foreach(var m in Object.FindObjectsByType<MonoBehaviour>().OrderBy(m=>m is CheckpointTrack?0:m is KartController?2:3)){ if(m==null||skip.Contains(m.GetType())) continue; if(m.GetType().Namespace==null||!m.GetType().Namespace.StartsWith("HarborRacer")) continue; var a=m.GetType().GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance); if(a!=null) a.Invoke(m,null); }
  foreach(var r in Object.FindObjectsByType<RacerProgress>()){ var pk=r.GetComponent<PlayerKartInput>(); if(pk) pk.enabled=false; if(!r.isPlayer){ r.Kart.Body.isKinematic=true; r.transform.position=new Vector3(0,900,0); r.Kart.controlsLocked=true; } }
  var fu=typeof(KartController).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance); Physics.simulationMode=SimulationMode.Script;
  var kart=Object.FindObjectsByType<RacerProgress>().First(r=>r.isPlayer).Kart; var rb=kart.Body;
  var line=GameObject.Find("Markers/Paths/MarioRaceEndPath").GetComponent<TrackPath>();
  var pd=kart.gameObject.AddComponent<PathDriverInput>(); pd.kart=kart; pd.path=line; pd.racerAI=false; pd.cruiseSpeed=85f; kart.Input=pd;
  int startNode=31; Vector3 sp=line.GetNode(startNode).position; if(Physics.Raycast(sp+Vector3.up*6,Vector3.down,out var h,20,HarborRacer.Layers.GroundMask,QueryTriggerInteraction.Ignore)) sp=h.point;
  Vector3 d=Vector3.ProjectOnPlane(line.GetNode(startNode+1).position-line.GetNode(startNode).position,Vector3.up).normalized;
  rb.isKinematic=true; kart.transform.SetPositionAndRotation(sp+Vector3.up*1.0f,Quaternion.LookRotation(d,Vector3.up)); rb.isKinematic=false; rb.linearVelocity=d*70f; rb.rotation=kart.transform.rotation; kart.CurrentSpeed=70; kart.controlsLocked=false; pd.nodeIndex=startNode+1; Physics.SyncTransforms();
  bool ag=false; int enters=0, exits=0; float maxTilt=0; float lastExitT=-1; float tiltAfterExit=0; int respawn=0; float t0=0;
  for(int i=0;i<2600;i++){ fu.Invoke(kart,null); Physics.Simulate(0.02f); float t=i*0.02f; var st=kart.State;
   float tilt=Vector3.Angle(kart.transform.up,Vector3.up); maxTilt=Mathf.Max(maxTilt,tilt);
   if(st.AntiGravity&&!ag){ ag=true; enters++; sb.AppendLine($"  t={t:F1} ENTER anti-gravity at ({kart.transform.position.x:F0},{kart.transform.position.y:F0},{kart.transform.position.z:F0}) speed={kart.ForwardSpeed:F0}"); }
   if(!st.AntiGravity&&ag){ ag=false; exits++; lastExitT=t; sb.AppendLine($"  t={t:F1} EXIT  anti-gravity at ({kart.transform.position.x:F0},{kart.transform.position.y:F0},{kart.transform.position.z:F0}) tilt={tilt:F0} deg"); }
   if(lastExitT>0&&t>lastExitT+2f&&tiltAfterExit==0) tiltAfterExit=tilt+0.001f;
   if(kart.Respawn.Respawning){ respawn++; sb.AppendLine($"  t={t:F1} RESPAWN at ({kart.transform.position.x:F0},{kart.transform.position.y:F0},{kart.transform.position.z:F0})"); break; }
   if(pd.nodeIndex>=48) break; t0=t; }
  sb.AppendLine($"anti-gravity enters={enters} exits={exits} maxTilt={maxTilt:F0} deg tiltTwoSecondsAfterLastExit={tiltAfterExit:F0} deg respawns={respawn} reachedNode={pd.nodeIndex} after {t0:F1}s");
  Physics.simulationMode=SimulationMode.FixedUpdate; return sb.ToString(); } }
