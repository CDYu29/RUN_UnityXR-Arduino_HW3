using System.Collections.Generic;
using UnityEngine;

public class TempleRunMonster : MonoBehaviour
{
    [Header("連結玩家與安全屋大門")]
    public TempleRunPlayer player;          // 拖入 XR Origin (XR Rig)
    public AutoCloseDoor safeDoor;          // 選填：拖入 DoorTrigger，門關上後怪物會被擋在門外

    [Header("怪物延遲出發設定")]
    public float startDelay = 2.5f;          // 開場後延遲幾秒怪物才開始出發追逐
    public bool waitForPlayerStart = false;  // 勾選後，會等玩家真正開始移動（如踩下第一步）才開始倒數出發

    [Header("怪物起始距離與追逐速度")]
    public float startBehindDistance = 6.5f; // 開場在玩家身後幾公尺（VR 轉頭就能看見）
    public float chaseSpeed = 6.5f;          // 怪物奔跑速度（數值越小跑越慢）
    public float maxBehindDistance = 12f;    // 最大落後距離（避免被甩太遠失去壓迫感，設 0 則不自動拉近）
    public float catchDistance = 1.3f;       // 距離玩家多少公尺內算抓到玩家
    public float turnRotateSpeed = 720f;     // 過彎轉向速度（度/秒）

    [Header("動畫控制（選填）")]
    public Animator monsterAnimator;         // 若骷髏有掛 Animator，可填寫跑步參數名稱
    public string runBoolParam = "IsRunning";

    private TrackNode currentSegStart;
    private TrackNode currentSegEnd;
    private TrackNode lastObservedPlayerTarget;
    private readonly Queue<TrackNode> pathQueue = new Queue<TrackNode>();

    private bool isChasing = false;
    private bool hasStartedChasing = false;
    private float delayTimer = 0f;

    void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<TempleRunPlayer>();
        }

        if (player == null || player.startNode == null || player.firstTargetNode == null)
        {
            Debug.LogWarning("TempleRunMonster: 找不到玩家或起點節點，怪物無法啟動追逐。");
            return;
        }

        // 1. 初始化怪物的第一段路線
        currentSegStart = player.startNode;
        currentSegEnd = player.firstTargetNode;
        lastObservedPlayerTarget = player.firstTargetNode;

        // 2. 開場自動將怪物擺放到起點後方 startBehindDistance 處
        Vector3 startXZ = GetFlatPos(currentSegStart.transform.position);
        Vector3 endXZ = GetFlatPos(currentSegEnd.transform.position);
        Vector3 forwardDir = (endXZ - startXZ).normalized;

        Vector3 spawnPos = startXZ - forwardDir * startBehindDistance;
        transform.position = new Vector3(spawnPos.x, transform.position.y, spawnPos.z);
        transform.rotation = Quaternion.LookRotation(forwardDir);

        // 3. 設定延遲出發計時器，尚未出發前保持待機
        delayTimer = startDelay;
        hasStartedChasing = false;
        isChasing = true;
        SetRunningAnimation(false);
    }

    void Update()
    {
        if (player != null && !player.hasGameStarted) return;

        if (!isChasing || player == null) return;

        // 1. 若玩家已失敗、成功過關，或安全屋大門已關閉，怪物停止追逐
        if (player.isGameOver || player.isVictory || (safeDoor != null && safeDoor.isClosed))
        {
            StopChasing();
            return;
        }

        // 2. 處理延遲出發倒數
        if (!hasStartedChasing)
        {
            if (waitForPlayerStart && player.forwardSpeed <= 0.1f)
            {
                return;
            }

            delayTimer -= Time.deltaTime;
            if (delayTimer <= 0f)
            {
                hasStartedChasing = true;
                SetRunningAnimation(true);
            }
            else
            {
                return;
            }
        }

        // 3. 偵測前方是否撞上已關閉的實體門板
        if (IsBlockedByClosedDoor())
        {
            StopChasing();
            return;
        }

        // 4. 記錄玩家在路口選擇的新目標節點，確保怪物走完全相同的路線
        if (player.TargetNode != null && player.TargetNode != lastObservedPlayerTarget)
        {
            pathQueue.Enqueue(player.TargetNode);
            lastObservedPlayerTarget = player.TargetNode;
        }

        // 5. 沿著路徑節點中心線前進與轉彎
        MoveAlongNodes();

        // 6. 檢查是否追上玩家
        float distToPlayer = Vector3.Distance(GetFlatPos(transform.position), GetFlatPos(player.transform.position));
        if (distToPlayer <= catchDistance)
        {
            StopChasing();
            player.TriggerGameOver("被後面的怪物追上了（失敗）！");
        }
    }

    private void MoveAlongNodes()
    {
        if (currentSegStart == null || currentSegEnd == null) return;

        Vector3 startXZ = GetFlatPos(currentSegStart.transform.position);
        Vector3 endXZ = GetFlatPos(currentSegEnd.transform.position);
        Vector3 segDir = (endXZ - startXZ).normalized;
        float segLength = Vector3.Distance(startXZ, endXZ);

        if (segDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(segDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnRotateSpeed * Time.deltaTime);
        }

        float effectiveSpeed = chaseSpeed;
        if (maxBehindDistance > 0f)
        {
            float distToPlayer = Vector3.Distance(GetFlatPos(transform.position), GetFlatPos(player.transform.position));
            if (distToPlayer > maxBehindDistance && player.forwardSpeed > 0.1f)
            {
                effectiveSpeed = Mathf.Max(chaseSpeed, player.forwardSpeed * 1.15f);
            }
        }

        Vector3 currentXZ = GetFlatPos(transform.position);
        float currentProgress = Vector3.Dot(currentXZ - startXZ, segDir);
        float nextProgress = currentProgress + effectiveSpeed * Time.deltaTime;

        if (nextProgress >= segLength)
        {
            if (pathQueue.Count > 0)
            {
                currentSegStart = currentSegEnd;
                currentSegEnd = pathQueue.Dequeue();
            }
            else
            {
                Vector3 endPos = startXZ + segDir * segLength;
                transform.position = new Vector3(endPos.x, transform.position.y, endPos.z);
                StopChasing();
            }
        }
        else
        {
            Vector3 nextPosXZ = startXZ + segDir * nextProgress;
            transform.position = new Vector3(nextPosXZ.x, transform.position.y, nextPosXZ.z);
        }
    }

    private bool IsBlockedByClosedDoor()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, 1.1f))
        {
            if (!hit.collider.isTrigger && hit.collider.name.ToLower().Contains("door"))
            {
                return true;
            }
        }
        return false;
    }

    private void StopChasing()
    {
        isChasing = false;
        SetRunningAnimation(false);
    }

    private void SetRunningAnimation(bool running)
    {
        if (monsterAnimator != null && !string.IsNullOrEmpty(runBoolParam))
        {
            monsterAnimator.SetBool(runBoolParam, running);
        }
    }

    private Vector3 GetFlatPos(Vector3 pos) => new Vector3(pos.x, 0f, pos.z);
}