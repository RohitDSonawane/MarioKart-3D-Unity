using System.Text;
using UnityEngine;
using HarborRacer.Kart;

public static class PoseCheck
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var pa in Object.FindObjectsByType<DriverPoseAnimator>(FindObjectsInactive.Include))
        {
            string N(AnimationClip c) => c ? c.name.Substring(c.name.IndexOf('|') + 1) : "-";
            sb.AppendLine(pa.transform.root.name.PadRight(10) + " root=" + (pa.animRoot ? pa.animRoot.name : "NULL") + " idle=" + N(pa.idle) + " L=" + N(pa.turnLeft) + " R=" + N(pa.turnRight) + " item=" + N(pa.itemIdle) + " itemL=" + N(pa.itemTurnLeft) + " itemR=" + N(pa.itemTurnRight) + " throwF=" + N(pa.throwForward) + " throwB=" + N(pa.throwBack));
        }
        int strays = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)) if (t.name.StartsWith("Carried_") || t.name == "Trolley(Clone)") strays++;
        sb.AppendLine("strays " + strays + ", scene dirty " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);
        return sb.ToString();
    }
}
