using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AutoCloseDoor : MonoBehaviour
{
    [Header("門板物件與角度設定")]
    public Transform doorTransform;     // 拖入 Hierarchy 裡的 door 物件
    public float openYAngle = 0f;       // 開門時的 Y 軸角度（若開錯方向請改成 180）
    public float closedYAngle = 90f;    // 關門時的 Y 軸角度（對齊門框的角度）

    [Header("關門速度與延遲")]
    public float closeSpeed = 540f;     // 關門旋轉速度（度/秒，數值越大關門越快）
    public float closeDelay = 0.08f;    // 玩家通過門檻後延遲幾秒再關門（避免夾到玩家）

    [Header("狀態檢視（可提供給怪物腳本判斷）")]
    public bool isClosed = false;

    private bool isTriggered = false;
    private float delayTimer = 0f;

    void Start()
    {
        // 確保觸發區為 Trigger，不會擋住玩家跑過去
        BoxCollider triggerBox = GetComponent<BoxCollider>();
        triggerBox.isTrigger = true;

        // 遊戲開始時，自動把門打開
        if (doorTransform != null)
        {
            Vector3 euler = doorTransform.localEulerAngles;
            doorTransform.localEulerAngles = new Vector3(euler.x, openYAngle, euler.z);
        }
    }

    void Update()
    {
        if (!isTriggered || isClosed || doorTransform == null) return;

        // 等待極短延遲，確保玩家身體完全進門後才關門
        if (delayTimer > 0f)
        {
            delayTimer -= Time.deltaTime;
            return;
        }

        // 快速平滑旋轉門板到關門角度
        Vector3 euler = doorTransform.localEulerAngles;
        float nextY = Mathf.MoveTowardsAngle(euler.y, closedYAngle, closeSpeed * Time.deltaTime);
        doorTransform.localEulerAngles = new Vector3(euler.x, nextY, euler.z);

        if (Mathf.Abs(Mathf.DeltaAngle(nextY, closedYAngle)) < 0.5f)
        {
            doorTransform.localEulerAngles = new Vector3(euler.x, closedYAngle, euler.z);
            isClosed = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;

        // 自動辨識玩家（支援 TempleRunPlayer、XR Origin 或 Player Tag）
        bool isPlayer = other.GetComponentInParent<TempleRunPlayer>() != null
                        || other.transform.root.name.Contains("XR Origin")
                        || other.CompareTag("Player");

        if (isPlayer)
        {
            isTriggered = true;
            delayTimer = closeDelay;
        }
    }
}
