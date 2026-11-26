using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 確保停止播放時比 Ardity 的 SerialController 更早執行，讓關燈指令順利送出
[DefaultExecutionOrder(-100)]
public class ArduinoTempleController : MonoBehaviour
{
    [Header("1. 核心元件連結")]
    public TempleRunPlayer player;
    public SerialController serialController;

    [Header("2. 無遮罩 UI 介面連結")]
    public GameObject startGroup;         // 開始畫面透明群組（內含開始按鈕）
    public TMP_Text countdownText;        // 選填：畫面中央同步顯示 5~1 的倒數文字
    public TMP_Text timerText;            // 左上角計時器文字
    public GameObject gameOverGroup;      // 失敗純字樣群組（無遮罩，含「按 R 重啟」）
    public GameObject victoryGroup;       // 成功純字樣群組（無遮罩，含「按 R 重啟」）

    [Header("3. 第三人稱結算字樣延遲設定")]
    [Tooltip("失敗或成功切換到第三人稱運鏡後，等待幾秒才浮現結算字樣（建議 3 ~ 5 秒）")]
    public float endScreenDelay = 3.5f;

    [Header("4. 硬體與快捷鍵設定")]
    [Tooltip("取消勾選：搖桿撥一下換一格跑道；勾選：推著不放才靠邊")]
    public bool returnToCenterOnRelease = false;
    [Tooltip("結算時按下實體中間按鈕（跳躍鍵）可直接重開關卡")]
    public bool restartWithJumpButton = true;

    private int currentLedState = -1;     // 0=全滅, 1=紅燈(失敗), 2=藍燈(成功)
    private bool isCountingDown = false;
    private bool isQuittingOrStopping = false;
    private float elapsedPlayTime = 0f;
    private Coroutine endScreenCoroutine;

    void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged += OnEditorPauseStateChanged;
#endif
    }

    void Start()
    {
        if (player == null)
            player = GetComponent<TempleRunPlayer>();

        if (serialController == null)
            serialController = FindFirstObjectByType<SerialController>();

        // 初始化遊戲與 UI 狀態
        if (player != null) player.hasGameStarted = false;
        if (startGroup != null) startGroup.SetActive(true);
        if (countdownText != null) countdownText.text = "";
        if (gameOverGroup != null) gameOverGroup.SetActive(false);
        if (victoryGroup != null) victoryGroup.SetActive(false);

        elapsedPlayTime = 0f;
        UpdateTimerUI(0f);

        // 開局先熄滅 LED 與七段顯示器
        SetArduinoLed(0);
    }

    void Update()
    {
        if (player == null || isQuittingOrStopping) return;

        // 1. 倒數結束正式開跑後，更新左上角計時器（失敗或過關時停止計時）
        if (player.hasGameStarted && !player.isGameOver && !player.isVictory)
        {
            elapsedPlayTime += Time.deltaTime;
            UpdateTimerUI(elapsedPlayTime);
        }

        // 2. 偵測失敗或過關：立刻亮紅燈/藍燈，並等待 3.5 秒運鏡結束後才顯示純字樣
        if (player.isGameOver && currentLedState != 1)
        {
            SetArduinoLed(1);
            if (endScreenCoroutine != null) StopCoroutine(endScreenCoroutine);
            endScreenCoroutine = StartCoroutine(ShowEndTextAfterDelay(gameOverGroup));
        }
        else if (player.isVictory && currentLedState != 2)
        {
            SetArduinoLed(2);
            if (endScreenCoroutine != null) StopCoroutine(endScreenCoroutine);
            endScreenCoroutine = StartCoroutine(ShowEndTextAfterDelay(victoryGroup));
        }
    }

    private IEnumerator ShowEndTextAfterDelay(GameObject targetGroup)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, endScreenDelay));
        if (targetGroup != null)
        {
            targetGroup.SetActive(true);
        }
    }

    // 綁定給畫面上「開始遊戲」Button 的 OnClick () 事件
    public void OnClickStartButton()
    {
        if (isCountingDown || (player != null && player.hasGameStarted)) return;
        StartCoroutine(CountdownAndStartRoutine());
    }

    private IEnumerator CountdownAndStartRoutine()
    {
        isCountingDown = true;

        // 同步控制 Arduino 七段顯示器與畫面文字倒數 5 ~ 1
        for (int i = 5; i >= 1; i--)
        {
            SendRawCommand(i.ToString());
            if (countdownText != null) countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        // 倒數至 0 瞬間起跑並隱藏開始群組
        SendRawCommand("0");
        if (countdownText != null) countdownText.text = "GO!";
        if (startGroup != null) startGroup.SetActive(false);
        if (player != null) player.hasGameStarted = true;

        yield return new WaitForSeconds(0.5f);
        SendRawCommand("-"); // 熄滅七段顯示器
        if (countdownText != null) countdownText.text = "";
        isCountingDown = false;
    }

    private void UpdateTimerUI(float timeSeconds)
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(timeSeconds / 60f);
        float seconds = timeSeconds % 60f;
        timerText.text = string.Format("{0:00}:{1:00.00}", minutes, seconds);
    }

#if UNITY_EDITOR
    private void OnEditorPauseStateChanged(PauseState state)
    {
        if (state == PauseState.Paused)
            SendRawCommand("O");
        else if (state == PauseState.Unpaused)
            SyncCurrentLedState();
    }
#endif

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SendRawCommand("O");
        else SyncCurrentLedState();
    }

    void OnApplicationQuit()
    {
        TurnOffHardwareBeforeExit();
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.pauseStateChanged -= OnEditorPauseStateChanged;
#endif
        TurnOffHardwareBeforeExit();
    }

    private void TurnOffHardwareBeforeExit()
    {
        if (isQuittingOrStopping) return;
        isQuittingOrStopping = true;

        if (serialController != null)
        {
            serialController.SendSerialMessage("O");
            Thread.Sleep(80);
        }
    }

    private void SyncCurrentLedState()
    {
        if (player == null)
        {
            SetArduinoLed(0);
            return;
        }
        int targetState = player.isGameOver ? 1 : (player.isVictory ? 2 : 0);
        SetArduinoLed(targetState);
    }

    private void SetArduinoLed(int state)
    {
        currentLedState = state;
        if (state == 1) SendRawCommand("R");
        else if (state == 2) SendRawCommand("B");
        else SendRawCommand("O");
    }

    private void SendRawCommand(string cmd)
    {
        if (serialController == null) return;
        serialController.SendSerialMessage(cmd);
    }

    void OnMessageArrived(string msg)
    {
        if (player == null || string.IsNullOrEmpty(msg)) return;
        string command = msg.Trim();

        // 結算狀態下，按下實體中間鈕可直接重開
        if ((player.isGameOver || player.isVictory) && restartWithJumpButton && command == "JUMP")
        {
            SetArduinoLed(0);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (!player.hasGameStarted) return;

        switch (command)
        {
            case "TURN_L": player.TurnLeft(); break;
            case "JUMP": player.Jump(); break;
            case "TURN_R": player.TurnRight(); break;
            case "LANE_L":
                if (returnToCenterOnRelease) player.SetLane(-1);
                else player.MoveLeft();
                break;
            case "LANE_C":
                if (returnToCenterOnRelease) player.SetLane(0);
                break;
            case "LANE_R":
                if (returnToCenterOnRelease) player.SetLane(1);
                else player.MoveRight();
                break;
        }
    }

    void OnConnectionEvent(bool success)
    {
        if (success)
        {
            Debug.Log("Arduino 控制器連線成功！");
            SyncCurrentLedState();
        }
        else
        {
            Debug.LogWarning("Arduino 控制器已斷線或連線失敗，請檢查 COM Port。");
        }
    }
}