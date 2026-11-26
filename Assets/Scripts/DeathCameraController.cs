using UnityEngine;

public class DeathCameraController : MonoBehaviour
{
    [Header("連結玩家、怪物、大門與攝影機（必設）")]
    public TempleRunPlayer player;          // 拖入 XR Origin (XR Rig)
    public Transform monsterTransform;      // 拖入場景中的怪物
    public Transform doorTransform;         // 拖入 safe 底下的 DoorHinge（留空會自動尋找 DoorHinge）
    public Camera firstPersonCamera;        // 拖入 XR Origin 底下的 Main Camera
    public Camera thirdPersonCamera;        // 拖入新建的 DeathCamera
    public Transform uiCanvas;              // 新增：拖入 Main Camera 底下的 Canvas

    [Header("第三人稱時恢復顯示騎士頭部（選填）")]
    public GameObject knightHead;           // 拖入原本在第一人稱隱藏的騎士頭部物件

    [Header("1. 鏡頭平滑滑動共用設定")]
    public float transitionDuration = 0.6f;  // 鏡頭滑過去的時間（秒）
    public float lookAtHeight = 1.0f;        // 鏡頭注視高度
    public bool avoidWalls = false;          // 預設關閉，避免被安全屋牆壁或拱門卡住鏡頭

    [Header("2. 失敗（Game Over）鏡頭與怪物追上演出")]
    public float deathCameraHeight = 4.5f;   // 失敗時鏡頭高度
    public float deathCameraBack = 4.5f;     // 失敗時鏡頭後方距離
    public float deathCameraSide = 2.5f;     // 失敗時鏡頭側邊偏移
    public bool monsterRushIn = true;        // 失敗時讓怪物從後方衝上來抓主角
    public float rushStartDistance = 8.5f;   // 失敗瞬間怪物從主角身後幾公尺起跑
    public float monsterRushDelay = 0.15f;   // 鏡頭先轉過去幾秒後，怪物才開始衝上來
    public float monsterRushDuration = 1.1f; // 怪物衝過來花費的秒數
    public float catchStopDistance = 1.1f;   // 失敗時怪物停在主角身後幾公尺處

    [Header("3. 成功（Victory）俯視看到怪物在門外設定")]
    public float victoryCameraHeight = 6.8f; // 成功時鏡頭高度（拉高俯視屋內與門外）
    public float victoryCameraBack = 3.2f;   // 成功時鏡頭往門外方向退幾公尺
    public float victoryCameraSide = 0f;     // 成功時鏡頭側邊偏移（0 ＝ 正後方高處俯拍不偏斜）
    public float outsideDoorStopDist = 1.3f; // 怪物被關在門外距離門板幾公尺處
    public float victoryRushDist = 6.5f;     // 過關時怪物從門外幾公尺處衝到門口停下

    private bool isTransitioning = false;
    private bool isVictoryMode = false;
    private float transitionTimer = 0f;

    private Vector3 camStartPos;
    private Quaternion camStartRot;
    private Vector3 fixedFocusPoint;
    private Vector3 fixedDesiredCamPos;

    private Vector3 monsterStartPos;
    private Vector3 monsterEndPos;
    private Animator monsterAnim;
    private string monsterRunParam = "IsRunning";

    void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<TempleRunPlayer>();

        if (doorTransform == null)
        {
            GameObject hingeObj = GameObject.Find("DoorHinge");
            if (hingeObj != null) doorTransform = hingeObj.transform;
        }

        if (firstPersonCamera != null)
            firstPersonCamera.enabled = true;

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.enabled = false;
            AudioListener extraListener = thirdPersonCamera.GetComponent<AudioListener>();
            if (extraListener != null) extraListener.enabled = false;
        }

        if (monsterTransform != null)
        {
            TempleRunMonster monsterScript = monsterTransform.GetComponent<TempleRunMonster>();
            if (monsterScript != null)
            {
                monsterAnim = monsterScript.monsterAnimator;
                if (!string.IsNullOrEmpty(monsterScript.runBoolParam))
                    monsterRunParam = monsterScript.runBoolParam;
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        if (!isTransitioning && (player.isGameOver || player.isVictory))
        {
            isVictoryMode = player.isVictory;
            StartSmoothTransition();
        }

        if (isTransitioning)
        {
            UpdateSmoothTransition();
        }
    }

    private void StartSmoothTransition()
    {
        isTransitioning = true;
        transitionTimer = 0f;

        if (knightHead != null)
            knightHead.SetActive(true);

        Vector3 playerPos = player.transform.position;
        Vector3 flatForward = new Vector3(player.transform.forward.x, 0f, player.transform.forward.z).normalized;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

        // 1. 計算門洞正中央座標（修正 DoorHinge 在側邊門軸導致偏移的問題）
        Vector3 doorCenterXZ = GetAlignedDoorCenter(playerPos, flatForward);

        // 2. 計算鏡頭目標位置與注視中心點
        if (isVictoryMode)
        {
            Vector3 midPoint = Vector3.Lerp(new Vector3(playerPos.x, 0f, playerPos.z), doorCenterXZ, 0.55f);
            midPoint.y = playerPos.y;

            fixedFocusPoint = midPoint + Vector3.up * lookAtHeight;
            fixedDesiredCamPos = midPoint
                                 - flatForward * victoryCameraBack
                                 + flatRight * victoryCameraSide
                                 + Vector3.up * victoryCameraHeight;
        }
        else
        {
            fixedFocusPoint = playerPos + Vector3.up * lookAtHeight;
            fixedDesiredCamPos = playerPos
                                 - flatForward * deathCameraBack
                                 + flatRight * deathCameraSide
                                 + Vector3.up * deathCameraHeight;
        }

        if (avoidWalls && Physics.Linecast(fixedFocusPoint, fixedDesiredCamPos, out RaycastHit hit))
        {
            if (!hit.collider.isTrigger)
            {
                fixedDesiredCamPos = hit.point + hit.normal * 0.25f;
            }
        }

        // 3. 無縫切換攝影機
        // 3. 無縫切換攝影機
        if (firstPersonCamera != null && thirdPersonCamera != null)
        {
            camStartPos = firstPersonCamera.transform.position;
            camStartRot = firstPersonCamera.transform.rotation;

            thirdPersonCamera.transform.position = camStartPos;
            thirdPersonCamera.transform.rotation = camStartRot;

            firstPersonCamera.enabled = false;
            thirdPersonCamera.enabled = true;

            // 將 UI Canvas 同步轉移到第三人稱攝影機前方，保留計時器與結束字樣
            if (uiCanvas != null)
            {
                Vector3 savedLocalPos = uiCanvas.localPosition;
                Quaternion savedLocalRot = uiCanvas.localRotation;
                Vector3 savedLocalScale = uiCanvas.localScale;

                uiCanvas.SetParent(thirdPersonCamera.transform, false);
                uiCanvas.localPosition = savedLocalPos;
                uiCanvas.localRotation = savedLocalRot;
                uiCanvas.localScale = savedLocalScale;
            }
        }

        // 4. 設定怪物沿走廊正中央直線衝刺的起點與終點
        if (monsterTransform != null && monsterRushIn)
        {
            float monsterY = monsterTransform.position.y;
            Vector3 currentMonsterPos = monsterTransform.position;

            if (isVictoryMode)
            {
                // 終點對齊「門洞正中央外側」，不再偏移向側邊門軸
                Vector3 stopOutsideXZ = doorCenterXZ - flatForward * outsideDoorStopDist;
                monsterEndPos = new Vector3(stopOutsideXZ.x, monsterY, stopOutsideXZ.z);

                Vector3 toMonsterFlat = new Vector3(currentMonsterPos.x - stopOutsideXZ.x, 0f, currentMonsterPos.z - stopOutsideXZ.z);
                float distBehindDoor = Vector3.Dot(toMonsterFlat, -flatForward);
                float lateralOffset = Mathf.Abs(Vector3.Dot(toMonsterFlat, flatRight));

                // 只有當怪物已經轉進門前直路（左右偏移 < 1.0 公尺）才從原位衝刺，否則直接對齊走廊中心起跑
                if (distBehindDoor >= 0.5f && distBehindDoor <= victoryRushDist + 3f && lateralOffset < 1.0f)
                {
                    monsterStartPos = currentMonsterPos;
                }
                else
                {
                    Vector3 spawnXZ = stopOutsideXZ - flatForward * victoryRushDist;
                    monsterStartPos = new Vector3(spawnXZ.x, monsterY, spawnXZ.z);
                    monsterTransform.position = monsterStartPos;
                }
            }
            else
            {
                Vector3 toMonsterFlat = new Vector3(currentMonsterPos.x - playerPos.x, 0f, currentMonsterPos.z - playerPos.z);
                float distBehind = Vector3.Dot(toMonsterFlat, -flatForward);
                float lateralOffset = Mathf.Abs(Vector3.Dot(toMonsterFlat, flatRight));

                if (distBehind >= rushStartDistance - 1.5f && distBehind <= rushStartDistance + 3f && lateralOffset < 1.0f)
                {
                    monsterStartPos = currentMonsterPos;
                }
                else
                {
                    Vector3 spawnXZ = playerPos - flatForward * rushStartDistance;
                    monsterStartPos = new Vector3(spawnXZ.x, monsterY, spawnXZ.z);
                    monsterTransform.position = monsterStartPos;
                }

                Vector3 endXZ = playerPos - flatForward * catchStopDistance;
                monsterEndPos = new Vector3(endXZ.x, monsterY, endXZ.z);
            }

            // 強制水平朝向，確保怪物身體完全直立不歪斜
            monsterTransform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);

            if (monsterAnim != null)
                monsterAnim.SetBool(monsterRunParam, true);
        }
    }

    private Vector3 GetAlignedDoorCenter(Vector3 playerPos, Vector3 flatForward)
    {
        Vector3 playerXZ = new Vector3(playerPos.x, 0f, playerPos.z);
        if (doorTransform == null)
        {
            return playerXZ - flatForward * 2.5f;
        }

        // 將側邊 DoorHinge 的位置投影回玩家前進的中心線上，得出門洞正中央的座標
        Vector3 toHingeXZ = new Vector3(doorTransform.position.x - playerPos.x, 0f, doorTransform.position.z - playerPos.z);
        float distBehindPlayer = Vector3.Dot(toHingeXZ, -flatForward);
        if (distBehindPlayer < 0.3f) distBehindPlayer = 2.5f;

        return playerXZ - flatForward * distBehindPlayer;
    }

    private void UpdateSmoothTransition()
    {
        transitionTimer += Time.deltaTime;

        float camRawT = Mathf.Clamp01(transitionTimer / Mathf.Max(0.05f, transitionDuration));
        float camSmoothT = Mathf.SmoothStep(0f, 1f, camRawT);

        if (thirdPersonCamera != null)
        {
            Vector3 nextCamPos = Vector3.Lerp(camStartPos, fixedDesiredCamPos, camSmoothT);
            thirdPersonCamera.transform.position = nextCamPos;

            Vector3 lookDir = fixedFocusPoint - nextCamPos;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetLookRot = Quaternion.LookRotation(lookDir);
                thirdPersonCamera.transform.rotation = Quaternion.Slerp(camStartRot, targetLookRot, camSmoothT);
            }
        }

        if (monsterTransform != null && monsterRushIn)
        {
            float rushElapsed = Mathf.Max(0f, transitionTimer - monsterRushDelay);
            float rushRawT = Mathf.Clamp01(rushElapsed / Mathf.Max(0.05f, monsterRushDuration));
            float rushT = Mathf.SmoothStep(0f, 1f, rushRawT);

            monsterTransform.position = Vector3.Lerp(monsterStartPos, monsterEndPos, rushT);

            if (rushRawT >= 1f && monsterAnim != null)
            {
                monsterAnim.SetBool(monsterRunParam, false);
            }
        }
    }
}