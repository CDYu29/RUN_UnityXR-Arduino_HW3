using UnityEngine;

public class TrackNode : MonoBehaviour
{
    public enum NodeEndType
    {
        Normal,   // 一般路段或轉角
        Goal,     // 成功終點（過關）
        DeadEnd   // 失敗死路（走錯路失敗）
    }

    [Header("末端屬性設定（一般轉角保持 Normal 即可）")]
    public NodeEndType endType = NodeEndType.Normal;

    [Header("連接下一個路段的節點（無該方向則留空 None）")]
    public TrackNode straightNode; // 直走的下一個節點
    public TrackNode leftNode;     // 左轉 90 度後的下一個節點
    public TrackNode rightNode;    // 右轉 90 度後的下一個節點

    private void OnDrawGizmos()
    {
        if (endType == NodeEndType.Goal)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.6f, 0.65f);
        }
        else if (endType == NodeEndType.DeadEnd)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.6f, 0.65f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.5f, 0.4f);
        }

        Gizmos.color = Color.green;
        if (straightNode != null)
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, straightNode.transform.position + Vector3.up * 0.5f);
        if (leftNode != null)
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, leftNode.transform.position + Vector3.up * 0.5f);
        if (rightNode != null)
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, rightNode.transform.position + Vector3.up * 0.5f);
    }
}
