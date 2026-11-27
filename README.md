# VR Run × Arduino 雙向硬體互動遊戲

這是一個結合 **Unity 6 VR（OpenXR）** 與 **Arduino 實體控制器** 的逃跑迷宮雙向互動專題。參考《神廟逃亡（Temple Run）》的形式。玩家透過實體雙軸搖桿切換三條跑道、三顆實體按鈕（由左至右依序為左轉、跳躍、右轉）操控角色閃避與轉向，遊戲內的 5 秒開跑倒數、失敗與過關狀態也會即時回傳至實體七段顯示器與紅藍 LED 燈號。

---

## 📋 開發環境、素材來源與軟硬體清單

### 1. 軟體版本與必要套件
* **遊戲引擎**：Unity `6000.0.80f1` (Unity 6 LTS)
* **VR 架構套件**：
  * OpenXR Plugin
  * XR Interaction Toolkit (XRI)
  * TextMeshPro (TMP)
* **序列埠通訊套件**：[Ardity](https://github.com/dwilches/Ardity)（用於 Unity 與 Arduino 雙向序列埠讀寫）
* **單晶片開發環境**：Arduino IDE 2.x

### 2. 開源 3D 美術素材與天空盒資源（CC0 免費授權）
* **3D 角色與神廟跑道模型（KayKit）**：
  * 官網總覽：[https://kaylousberg.com/game-assets](https://kaylousberg.com/game-assets)
  * itch.io 資源庫：[https://kaylousberg.itch.io/](https://kaylousberg.itch.io/)
  * 使用套件：KayKit Adventurers / Character Pack（包含玩家角色 `character_rogue` 與追逐怪物 `character_barbarian`）及 Dungeon / Platformer 環境道路套件。
* **360° 全景天空盒材質（Poly Haven - Pure Skies）**：
  * 網址：[https://polyhaven.com/hdris/pure-skies](https://polyhaven.com/hdris/pure-skies)
  * 用途：提供高解析度 360 度無遮擋天空 HDRI（`.exr` / `.hdr`），作為場景環境光與穹頂天空背景（`Skybox Material`）。

### 3. 硬體設備與零件
* **微控制器**：Arduino UNO（含 USB Type-B 傳輸線）
* **控制輸入元件**：
  * **雙軸搖桿模組（Joystick）× 1**：專責控制角色在左、中、右三條跑道之間橫向切換
  * **實體按壓開關（Push Button）× 3**（麵包板由左至右排列）：
    1. **第 1 顆（最左側）— 左轉按鈕**（`D2`）
    2. **第 2 顆（正中間）— 跳躍／重啟按鈕**（`D3`）
    3. **第 3 顆（最右側）— 右轉按鈕**（`D4`）
* **回饋輸出元件**：
  * **個位數七段顯示器（7-Segment Display）× 1**：預設為共陰極（`IS_COMMON_CATHODE = true`；若使用共陽極請於 `.ino` 改為 `false`）
  * **紅色 LED × 1**（遊戲失敗亮燈）+ 220Ω 限流電阻
  * **藍色 LED × 1**（遊戲過關亮燈）+ 220Ω 限流電阻
* **其他**：麵包板、杜邦線若干、PC VR 頭顯（支援 OpenXR，如 Meta Quest Link / SteamVR）或電腦螢幕模擬測試

---

## 🔌 硬體接線與序列埠（COM Port）設定

### 1. Arduino UNO 腳位接線對照表
*(程式碼內建 `INPUT_PULLUP` 上拉電阻，三顆按鈕一端接對應數位腳位、另一端直接接地 `GND` 即可，無需外接上拉電阻)*

| 硬體元件 | Arduino UNO 腳位 | 訊號方向 / 說明 |
| :--- | :--- | :--- |
| **第 1 顆按鈕（左，左轉）** | **`D2`**（`INPUT_PULLUP`） | 按下送出 `"TURN_L"`，抵達轉角時執行左轉 |
| **第 2 顆按鈕（中，跳躍／重啟）** | **`D3`**（`INPUT_PULLUP`） | 按下送出 `"JUMP"`；奔跑時跳躍、結算時重置關卡 |
| **第 3 顆按鈕（右，右轉）** | **`D4`**（`INPUT_PULLUP`） | 按下送出 `"TURN_R"`，抵達轉角時執行右轉 |
| **雙軸搖桿 X 軸（`VRx`）** | **`A0`**（類比輸入） | 撥動切換跑道：`<280` 送 `"LANE_L"`、`380~640` 送 `"LANE_C"`、`>740` 送 `"LANE_R"` |
| **雙軸搖桿電源（`+5V`）** | **`5V`** | 提供搖桿模組工作電壓 |
| **紅色 LED（失敗燈）** | **`D5`**（串接 220Ω 電阻） | 收到 Unity 指令 `'R'` 時亮起（Game Over） |
| **藍色 LED（成功燈）** | **`D6`**（串接 220Ω 電阻） | 收到 Unity 指令 `'B'` 時亮起（Victory） |
| **七段顯示器 `a` 段** | **`D7`**（建議串接限流電阻） | 顯示倒數數字 `5 → 0`（收到 `'0'~'9'` 顯示，收到 `'-'` 或 `'O'` 熄滅） |
| **七段顯示器 `b` 段** | **`D8`** | 同上 |
| **七段顯示器 `c` 段** | **`D9`** | 同上 |
| **七段顯示器 `d` 段** | **`D10`** | 同上 |
| **七段顯示器 `e` 段** | **`D11`** | 同上 |
| **七段顯示器 `f` 段** | **`D12`** | 同上 |
| **七段顯示器 `g` 段** | **`D13`** | 同上 |
| **共同接地（`GND`）** | **`GND`** | 3 顆按鈕另一端、搖桿 `GND`、LED 負極、共陰極七段顯示器 `COM` 皆接 `GND` |

### 2. 燒錄韌體與查找序列埠（COM Port）
1. 使用 Arduino IDE 開啟專案資料夾中的 `Arduino_Code/*.ino`，選擇開發板 **`Arduino Uno`** 並點擊 **Upload（上傳）**。
2. **重要**：上傳完成後，**務必關閉 Arduino IDE 的序列埠監控視窗（Serial Monitor）**，否則會佔用序列埠導致 Unity 無法讀取。
3. 查詢電腦分配的 COM 埠號：
   * **Windows**：在「開始」圖示按右鍵 $\rightarrow$ **裝置管理員** $\rightarrow$ 展開 **連接埠 (COM 和 LPT)**，查看 `Arduino Uno (COMx)` 或 `USB-SERIAL CH340 (COMx)` 的編號（例如 `COM7`）。
4. 將 COM 埠填入 Unity：
   * 在 Unity 編輯器左側 **Hierarchy** 點選 **`SerialController`** 物件。
   * 到右側 **Inspector** 找到 **`Port Name`** 欄位，修改為查到的埠號（例如 `COM7`），**`Baud Rate`** 保持 **`9600`**。

---

## 🎮 遊戲機制與流程

1. **開場待機**：進入遊戲後，角色與追逐怪物在起點原地待命，畫面正前方顯示懸浮 **Start** 按鈕（World Space UI 掛載於攝影機前方），所有實體 LED 與七段顯示器保持熄滅（`O` 狀態）。
2. **5 秒硬體倒數**：點擊畫面上的 **Start** 按鈕後，Unity 透過序列埠依序傳送 `'5'` $\rightarrow$ `'0'`，實體**七段顯示器同步顯示倒數**；歸零瞬間開始畫面隱藏，角色自動向前奔跑，左上角計時器開始跳動，後方怪物延遲起跑追逐。
3. **奔跑與實體控制器分工操作**：
   * **三跑道切換（雙軸搖桿 `A0`）**：左右撥動搖桿在左（`LANE_L`）、中（`LANE_C`）、右（`LANE_R`）三條跑道間橫向移動，閃避跑道上的障礙物。
   * **路口左轉（第 1 顆按鈕 `D2`）**：抵達左轉路口節點（`TrackNode`）時，按下最左側按鈕執行左轉。
   * **跳躍閃避（第 2 顆按鈕 `D3`）**：遇到低矮障礙物時按下正中間按鈕跳躍跨越。
   * **路口右轉（第 3 顆按鈕 `D4`）**：抵達右轉路口節點（`TrackNode`）時，按下最右側按鈕執行右轉。
4. **結算與第三人稱運鏡（無全螢幕遮罩）**：
   * **失敗（Game Over）**：撞擊障礙物、轉錯方向衝出跑道或被怪物追上時，Unity 發送 `'R'` 點亮麵包板**紅燈（`D5`）**，鏡頭平滑切換至第三人稱高處俯視（`DeathCamera`），左上角計時器定格保留，延遲數秒後浮現透明底色失敗字樣。
   * **過關（Victory）**：抵達終點安全屋並自動關門（`AutoCloseDoor`）將怪物擋在門外後，Unity 發送 `'B'` 點亮麵包板**藍燈（`D6`）**，切換第三人稱俯視門內外演出，保留最終通關時間並浮現勝利字樣。
5. **快速重新開始**：在結算畫面按下鍵盤 **`R`** 鍵或硬體**第 2 顆按鈕（中間跳躍／重啟鈕 `D3`）**，即可發送 `'O'` 自動熄燈並重置關卡。

---

## ▶️ 如何在電腦上運作

1. 從本頁面點擊 **`Code` $\rightarrow$ `Download ZIP`** 下載並解壓縮專案（或使用 GitHub Desktop Clone 至本機）。
2. 打開 **Unity Hub**，點擊 **`Add` $\rightarrow$ `Add project from disk`**，選擇解壓縮後的專案根目錄（確認版本為 **`6000.0.80f1`**，初次開啟時 Unity 會自動重建 `Library` 快取）。
3. 接上 Arduino UNO 的 USB 傳輸線，並依上述步驟確認 `SerialController` 的 `Port Name`（如 `COM7`）設定正確。
4. 在 Unity 專案視窗開啟 `Assets/Scenes/SampleScene.unity`。
5. 若使用 PC VR 頭顯，先啟動 SteamVR 或 Meta Quest Link；若僅用電腦螢幕測試，可直接點擊 Unity 上方的 **Play（播放鍵）** 啟動遊戲。

---

## 🛠️ 常見問題與排除方法（Troubleshooting）

### Q1：按下 Play 後 Console 出現 `Object reference not set`（`SerialThreadLines.SendToWire` 報錯）？
* **原因**：Unity 無法開啟指定的 COM 埠。
* **解法**：
  1. 確認 Arduino IDE 的 **序列埠監控視窗（Serial Monitor）已經關閉**。
  2. 重新插拔 USB 線後，到 Windows「裝置管理員」確認最新的 COM 埠號，並同步更新 `SerialController` 裡的 `Port Name`。

### Q2：七段顯示器倒數時數字筆畫相反（該亮的沒亮、不該亮的全亮）？
* **原因**：手邊使用的七段顯示器為「共陽極（COM 接 `5V`）」，而非程式預設的共陰極。
* **解法**：打開 `Arduino_Code/*.ino`，將第 13 行的 `const bool IS_COMMON_CATHODE = true;` 改為 `false` 並重新上傳至 Arduino UNO 即可。

### Q3：按下 Play 後 Console 出現 `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class`？
* **原因**：Unity 6 專案預設僅啟用新版 Input System，導致舊版按鍵偵測（如按 `R` 重啟）被阻擋。
* **解法**：前往上方選單 **`Edit` $\rightarrow$ `Project Settings` $\rightarrow$ `Player` $\rightarrow$ `Other Settings`**，將 **`Active Input Handling*`** 改為 **`Both`**，點擊 Apply 重啟編輯器即可。

### Q4：匯入 Ardity 後出現找不到 `System.IO.Ports` 的編譯錯誤？
* **原因**：Unity 6 預設的 .NET API 相容層級未包含序列埠函式庫。
* **解法**：前往 **`Edit` $\rightarrow$ `Project Settings` $\rightarrow$ `Player` $\rightarrow$ `Other Settings`**，將 **`Api Compatibility Level*`** 從 `.NET Standard 2.1` 切換為 **`.NET Framework`**。

### Q5：第一人稱奔跑時，畫面左邊或中間被角色模型的後腦勺擋住？
* **原因**：玩家角色模型（`character_rogue`）在第一人稱視角下與 `Main Camera` 位置重疊。
* **解法**：在 Hierarchy 中展開 `XR Origin (XR Rig)`，取消勾選 `character_rogue` 將其隱藏（第三人稱結算鏡頭會透過 `DeathCameraController` 獨立呈現）。

### Q6：結束切換第三人稱俯視鏡頭時，計時器或失敗／勝利字樣消失了？
* **原因**：第一人稱 `Main Camera` 停用並切換至 `DeathCamera` 時，若 `Canvas` 仍留在舊攝影機底下會一併被隱藏。
* **解法**：確認 `DeathCameraController` 元件的 **`Ui Canvas`** 欄位已正確拖入 `Main Camera` 底下的 **`Canvas`**，切換鏡頭時腳本會自動將 UI 同步轉移至 `DeathCamera` 前方。

### Q7：天空盒顯示一片灰或需要替換 Poly Haven 天空材質？
* **原因**：場景環境設定尚未指定天空盒材質球，或匯入的 `.exr` 貼圖未設為立體球面（Cube）。
* **解法**：從 [Poly Haven Pure Skies](https://polyhaven.com/hdris/pure-skies) 下載 `.exr` 檔案匯入 Unity，在貼圖的 Inspector 中將 `Texture Shape` 改為 `Cube` 並按 `Apply`，接著建立 Shader 為 `Skybox/Cubemap` 的材質球並拖入 **`Window` $\rightarrow$ `Rendering` $\rightarrow$ `Lighting` $\rightarrow$ `Environment` $\rightarrow$ `Skybox Material`** 欄位即可[cite: 15]。
