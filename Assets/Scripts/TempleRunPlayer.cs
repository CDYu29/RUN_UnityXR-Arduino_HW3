using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class TempleRunPlayer : MonoBehaviour
{
    [Header("起點與第一段目標節點（必設）")]
    public TrackNode startNode;             // 起點正中央的節點
    public TrackNode firstTargetNode;       // 第一個轉角正中央的節點

    [Header("奔跑與三跑道設定")]
    public float forwardSpeed = 7f;         // 初始奔跑速度
    public float speedAcceleration = 0.05f; // 隨時間微幅加速（設為 0 則固定速度）
    public float laneDistance = 2f;         // 左(-2) / 中(0) / 右(+2) 跑道間距（嚴格 2 單位）
    public float laneSwitchSpeed = 16f;     // 換道與過彎置中的平滑速度

    [Header("轉彎、跳躍與滑行設定")]
    public float turnWindow = 3.5f;         // 距離轉角多少單位內允許轉彎
    public float turnBufferTime = 0.4f;     // 提早按轉彎鍵的預輸入緩衝時間
    public float turnRotateSpeed = 720f;    // 轉彎視角旋轉速度（度/秒）
    public bool failOnStraightTurn = true;  // 在沒有路口的直線中間誤按 A/D 是否也直接判定失敗
    public float jumpForce = 5.2f;          // 跳躍力道
    public float gravity = -36f;            // 重力加速度
    public float slideDuration = 0.75f;     // 滑行持續秒數

    [Header("遊戲狀態（唯讀檢視）")]
    public bool hasGameStarted = false;
    public bool isVictory = false;
    public bool isGameOver = false;

    [Header("選用：成功與失敗提示介面（可先留空）")]
    public GameObject victoryUI;
    public GameObject gameOverUI;

    // 提供給後方追逐怪物的唯讀屬性
    public TrackNode CurrentNode => currentNode;
    public TrackNode TargetNode => targetNode;

    private CharacterController controller;
    private TrackNode currentNode;
    private TrackNode targetNode;

    private int currentLane = 0;            // -1 = 左(-2), 0 = 中(0), 1 = 右(+2)
    private float verticalVelocity = 0f;
    private Quaternion targetRotation;

    private int bufferedTurn = 0;           // -1 = 緩衝左轉, 1 = 緩衝右轉, 0 = 無
    private float bufferTimer = 0f;

    private bool isSliding = false;
    private float slideTimer = 0f;
    private float defaultHeight;
    private Vector3 defaultCenter;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.minMoveDistance = 0f;
        defaultHeight = controller.height;
        defaultCenter = controller.center;

        if (victoryUI != null) victoryUI.SetActive(false);
        if (gameOverUI != null) gameOverUI.SetActive(false);

        currentNode = startNode;
        targetNode = firstTargetNode;

        if (currentNode != null && targetNode != null)
        {
            Vector3 startPos = currentNode.transform.position;
            Vector3 targetPos = targetNode.transform.position;
            Vector3 dir = GetFlatDirection(startPos, targetPos);

            transform.position = new Vector3(startPos.x, transform.position.y, startPos.z);
            targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = targetRotation;
        }
        else
        {
            targetRotation = transform.rotation;
        }
    }


    void Update()
    {
        // 把原本的 if (Input.GetKeyDown(KeyCode.R)) 改成下面這行：
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }

        if (!hasGameStarted) return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartGame();
            return;
        }

        if (isVictory || isGameOver || currentNode == null || targetNode == null) return;

        HandleKeyboardInput();
        HandleSlideTimer();
        ProcessCornerAndTurns();
        MoveAlongTrack();
        CheckFallOut();
    }

    private void HandleKeyboardInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) MoveLeft();
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) MoveRight();

        if (Keyboard.current.aKey.wasPressedThisFrame) TurnLeft();
        else if (Keyboard.current.dKey.wasPressedThisFrame) TurnRight();

        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame) Jump();
        if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) Slide();
    }

    private void ProcessCornerAndTurns()
    {
        Vector3 startXZ = GetFlatPos(currentNode.transform.position);
        Vector3 endXZ = GetFlatPos(targetNode.transform.position);
        Vector3 segDir = (endXZ - startXZ).normalized;
        float segLength = Vector3.Distance(startXZ, endXZ);

        Vector3 playerXZ = GetFlatPos(transform.position);
        float progress = Vector3.Dot(playerXZ - startXZ, segDir);
        float remainingDist = segLength - progress;

        if (bufferedTurn != 0)
        {
            bufferTimer -= Time.deltaTime;

            if (remainingDist <= turnWindow && remainingDist >= -1.2f)
            {
                TrackNode nextBranch = (bufferedTurn == -1) ? targetNode.leftNode : targetNode.rightNode;
                if (nextBranch != null)
                {
                    SwitchToNextSegment(targetNode, nextBranch);
                    bufferedTurn = 0;
                    return;
                }
                else
                {
                    TriggerWrongTurn(bufferedTurn);
                    bufferedTurn = 0;
                    return;
                }
            }
            else if (bufferTimer <= 0f)
            {
                int wrongDir = bufferedTurn;
                bufferedTurn = 0;
                if (failOnStraightTurn)
                {
                    TriggerWrongTurn(wrongDir);
                    return;
                }
            }
        }

        if (remainingDist <= 0f)
        {
            if (targetNode.endType == TrackNode.NodeEndType.Goal)
            {
                TriggerVictory();
                return;
            }
            else if (targetNode.endType == TrackNode.NodeEndType.DeadEnd)
            {
                TriggerGameOver("走錯路了，抵達死路（失敗）！");
                return;
            }

            if (targetNode.straightNode != null)
            {
                SwitchToNextSegment(targetNode, targetNode.straightNode);
            }
            else if (remainingDist < -1.5f)
            {
                TriggerGameOver("轉角未及時轉彎，衝出跑道（失敗）！");
            }
        }
    }

    private void TriggerWrongTurn(int turnDir)
    {
        targetRotation *= Quaternion.Euler(0f, turnDir * 90f, 0f);
        transform.rotation = targetRotation;

        string dirText = (turnDir == -1) ? "左轉" : "右轉";
        TriggerGameOver($"轉彎方向錯誤（誤按{dirText}），撞牆或衝出跑道（失敗）！");
    }

    private void SwitchToNextSegment(TrackNode newStart, TrackNode newEnd)
    {
        currentNode = newStart;
        targetNode = newEnd;
        currentLane = 0;

        Vector3 newDir = GetFlatDirection(currentNode.transform.position, targetNode.transform.position);
        targetRotation = Quaternion.LookRotation(newDir);
    }

    private void MoveAlongTrack()
    {
        forwardSpeed += speedAcceleration * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnRotateSpeed * Time.deltaTime);

        Vector3 startXZ = GetFlatPos(currentNode.transform.position);
        Vector3 endXZ = GetFlatPos(targetNode.transform.position);
        Vector3 segDir = (endXZ - startXZ).normalized;
        Vector3 segRight = Vector3.Cross(Vector3.up, segDir).normalized;

        Vector3 currentXZ = GetFlatPos(transform.position);

        float currentProgress = Vector3.Dot(currentXZ - startXZ, segDir);
        float nextProgress = currentProgress + forwardSpeed * Time.deltaTime;
        Vector3 nextCenterXZ = startXZ + segDir * nextProgress;

        float currentLateral = Vector3.Dot(currentXZ - startXZ, segRight);
        float desiredLateral = currentLane * laneDistance;
        float nextLateral = Mathf.MoveTowards(currentLateral, desiredLateral, laneSwitchSpeed * Time.deltaTime);

        Vector3 nextPosXZ = nextCenterXZ + segRight * nextLateral;
        Vector3 horizontalMove = nextPosXZ - currentXZ;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        Vector3 verticalMove = Vector3.up * (verticalVelocity * Time.deltaTime);

        controller.Move(horizontalMove + verticalMove);
    }

    private void HandleSlideTimer()
    {
        if (!isSliding) return;

        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0f)
        {
            EndSlide();
        }
    }

    private void EndSlide()
    {
        isSliding = false;
        controller.height = defaultHeight;
        controller.center = defaultCenter;
    }

    private void CheckFallOut()
    {
        if (transform.position.y < -5f)
        {
            TriggerGameOver("掉落深淵（失敗）！");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            TriggerGameOver("撞到障礙物（失敗）！");
        }
    }

    public void TriggerVictory()
    {
        if (isVictory || isGameOver) return;
        isVictory = true;
        if (victoryUI != null) victoryUI.SetActive(true);
        Debug.Log("Victory: 成功抵達神廟終點！（按 R 鍵重新開始）");
    }

    public void TriggerGameOver(string reason)
    {
        if (isGameOver || isVictory) return;
        isGameOver = true;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        Debug.Log("Game Over: " + reason + "（按 R 鍵重新開始）");
    }

    private Vector3 GetFlatPos(Vector3 pos) => new Vector3(pos.x, 0f, pos.z);
    private Vector3 GetFlatDirection(Vector3 from, Vector3 to) => (GetFlatPos(to) - GetFlatPos(from)).normalized;

    public void MoveLeft()
    {
        if (!isGameOver && !isVictory) currentLane = Mathf.Clamp(currentLane - 1, -1, 1);
    }

    public void MoveRight()
    {
        if (!isGameOver && !isVictory) currentLane = Mathf.Clamp(currentLane + 1, -1, 1);
    }

    public void SetLane(int laneIndex)
    {
        if (!isGameOver && !isVictory) currentLane = Mathf.Clamp(laneIndex, -1, 1);
    }

    public void TurnLeft()
    {
        if (!isGameOver && !isVictory)
        {
            bufferedTurn = -1;
            bufferTimer = turnBufferTime;
        }
    }

    public void TurnRight()
    {
        if (!isGameOver && !isVictory)
        {
            bufferedTurn = 1;
            bufferTimer = turnBufferTime;
        }
    }

    public void Jump()
    {
        if (!isGameOver && !isVictory && controller.isGrounded)
        {
            if (isSliding) EndSlide();
            verticalVelocity = jumpForce;
        }
    }

    public void Slide()
    {
        if (!isGameOver && !isVictory && !isSliding)
        {
            isSliding = true;
            slideTimer = slideDuration;
            controller.height = defaultHeight * 0.45f;
            controller.center = new Vector3(defaultCenter.x, defaultCenter.y * 0.45f, defaultCenter.z);
            if (!controller.isGrounded) verticalVelocity = -12f;
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}