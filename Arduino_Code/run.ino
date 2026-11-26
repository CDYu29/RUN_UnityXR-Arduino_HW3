// 1. 輸入腳位（按鈕與搖桿）
const int BTN_LEFT_PIN  = 2;
const int BTN_JUMP_PIN  = 3;
const int BTN_RIGHT_PIN = 4;
const int JOY_X_PIN     = A0;

// 2. 輸出腳位（紅藍 LED）
const int LED_RED_PIN   = 5;
const int LED_BLUE_PIN  = 6;

// 3. 輸出腳位（個位數七段顯示器 a, b, c, d, e, f, g）
const int SEG_PINS[7] = {7, 8, 9, 10, 11, 12, 13};
// 若使用「共陽極（COM 接 5V）」七段顯示器，請將下面這行改為 false
const bool IS_COMMON_CATHODE = true;

const byte DIGIT_MAP[10][7] = {
  {1, 1, 1, 1, 1, 1, 0}, // 0
  {0, 1, 1, 0, 0, 0, 0}, // 1
  {1, 1, 0, 1, 1, 0, 1}, // 2
  {1, 1, 1, 1, 0, 0, 1}, // 3
  {0, 1, 1, 0, 0, 1, 1}, // 4
  {1, 0, 1, 1, 0, 1, 1}, // 5
  {1, 0, 1, 1, 1, 1, 1}, // 6
  {1, 1, 1, 0, 0, 0, 0}, // 7
  {1, 1, 1, 1, 1, 1, 1}, // 8
  {1, 1, 1, 1, 0, 1, 1}  // 9
};

// 搖桿輕撥感應門檻
const int LEFT_THRESHOLD   = 280;
const int CENTER_MIN       = 380;
const int CENTER_MAX       = 640;
const int RIGHT_THRESHOLD  = 740;

bool lastLeftState  = HIGH;
bool lastJumpState  = HIGH;
bool lastRightState = HIGH;
unsigned long lastDebounceTime = 0;
const unsigned long debounceDelay = 50;
int currentJoyZone = 0;

void displayDigit(int num) {
  for (int i = 0; i < 7; i++) {
    bool segOn = (num >= 0 && num <= 9) ? (DIGIT_MAP[num][i] == 1) : false;
    if (!IS_COMMON_CATHODE) segOn = !segOn;
    digitalWrite(SEG_PINS[i], segOn ? HIGH : LOW);
  }
}

void setup() {
  Serial.begin(9600);

  pinMode(BTN_LEFT_PIN, INPUT_PULLUP);
  pinMode(BTN_JUMP_PIN, INPUT_PULLUP);
  pinMode(BTN_RIGHT_PIN, INPUT_PULLUP);

  pinMode(LED_RED_PIN, OUTPUT);
  pinMode(LED_BLUE_PIN, OUTPUT);
  digitalWrite(LED_RED_PIN, LOW);
  digitalWrite(LED_BLUE_PIN, LOW);

  for (int i = 0; i < 7; i++) {
    pinMode(SEG_PINS[i], OUTPUT);
  }
  displayDigit(-1); // 開機預設全滅
}

void loop() {
  // 1. 接收 Unity 指令（LED 與七段顯示器）
  while (Serial.available() > 0) {
    char cmd = Serial.read();
    if (cmd == 'R') {
      digitalWrite(LED_RED_PIN, HIGH);
      digitalWrite(LED_BLUE_PIN, LOW);
    } else if (cmd == 'B') {
      digitalWrite(LED_RED_PIN, LOW);
      digitalWrite(LED_BLUE_PIN, HIGH);
    } else if (cmd == 'O') {
      digitalWrite(LED_RED_PIN, LOW);
      digitalWrite(LED_BLUE_PIN, LOW);
      displayDigit(-1);
    } else if (cmd >= '0' && cmd <= '9') {
      displayDigit(cmd - '0');
    } else if (cmd == '-') {
      displayDigit(-1);
    }
  }

  unsigned long now = millis();

  // 2. 讀取三顆按鈕
  bool leftState  = digitalRead(BTN_LEFT_PIN);
  bool jumpState  = digitalRead(BTN_JUMP_PIN);
  bool rightState = digitalRead(BTN_RIGHT_PIN);

  if (now - lastDebounceTime > debounceDelay) {
    if (lastLeftState == HIGH && leftState == LOW) {
      Serial.println("TURN_L");
      lastDebounceTime = now;
    }
    if (lastJumpState == HIGH && jumpState == LOW) {
      Serial.println("JUMP");
      lastDebounceTime = now;
    }
    if (lastRightState == HIGH && rightState == LOW) {
      Serial.println("TURN_R");
      lastDebounceTime = now;
    }
  }

  lastLeftState  = leftState;
  lastJumpState  = jumpState;
  lastRightState = rightState;

  // 3. 讀取搖桿左右軸
  int xVal = analogRead(JOY_X_PIN);
  int newZone = currentJoyZone;

  if (xVal < LEFT_THRESHOLD) newZone = -1;
  else if (xVal > RIGHT_THRESHOLD) newZone = 1;
  else if (xVal >= CENTER_MIN && xVal <= CENTER_MAX) newZone = 0;

  if (newZone != currentJoyZone) {
    currentJoyZone = newZone;
    if (currentJoyZone == -1) Serial.println("LANE_L");
    else if (currentJoyZone == 1) Serial.println("LANE_R");
    else Serial.println("LANE_C");
  }

  delay(10);
}