#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>

const char* ssid = "*****"; //
const char* password = "*******"; //

const char* baseUrl = "https://aq.ural-net.ru";

String email = "esp8266_user_123456777888@mail.com";
String passw = "StrongPass123456777000";

String apiToken = "";
String bearerToken = "";

unsigned long lastTokenTime = 0;
unsigned long lastSendTime = 0;
unsigned long lastStatusTime = 0;

const unsigned long tokenInterval = 30UL * 60UL * 1000UL; // 30 минут
const unsigned long sendInterval  = 60UL * 1000UL;        // 1 минута
const unsigned long statusInterval = 10UL * 1000UL;       // 10 секунд

WiFiClientSecure client;

// -------------------- WiFi --------------------
void connectWiFi() {
  WiFi.begin(ssid, password);
  Serial.print("Connecting");

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("\nConnected!");
}

// -------------------- STATUS --------------------
void printStatus() {
  Serial.println("------ STATUS ------");
  Serial.print("WiFi: ");
  Serial.println(WiFi.status() == WL_CONNECTED ? "OK" : "DISCONNECTED");

  Serial.print("RSSI: ");
  Serial.println(WiFi.RSSI());

  Serial.print("Free heap: ");
  Serial.println(ESP.getFreeHeap());

  Serial.print("Bearer set: ");
  Serial.println(bearerToken.length() > 0 ? "YES" : "NO");

  Serial.println("--------------------");
}

// -------------------- REGISTER --------------------
void registerUser() {
  HTTPClient https;

  String url = String(baseUrl) + "/api/auth/register";
  https.begin(client, url);
  https.addHeader("Content-Type", "application/json");

  StaticJsonDocument<200> doc;
  doc["email"] = email;
  doc["password"] = passw;

  String body;
  serializeJson(doc, body);

  int httpCode = https.POST(body);
  Serial.println("Register code: " + String(httpCode));

  if (httpCode == 200) {
    String payload = https.getString();

    StaticJsonDocument<512> res;
    deserializeJson(res, payload);

    apiToken = res["apiToken"].as<String>();
    Serial.println("API TOKEN: " + apiToken);
  }

  https.end();
}

// -------------------- GET TOKEN --------------------
void getToken() {
  HTTPClient https;

  String url = String(baseUrl) + "/api/auth/token";
  https.begin(client, url);

  https.addHeader("accept", "*/*");
  https.addHeader("Content-Type", "application/json");

  https.addHeader("Authorization", "Bearer eacda047-73f8-4878-a0fa-2cd3bcb305ab");

  StaticJsonDocument<200> doc;
  doc["apiToken"] = apiToken;

  String body;
  serializeJson(doc, body);

  int httpCode = https.POST(body);

  Serial.println("Token code: " + String(httpCode));

  if (httpCode == 200) {
    String payload = https.getString();

    StaticJsonDocument<512> res;
    deserializeJson(res, payload);

    bearerToken = res["accessToken"].as<String>();

    Serial.println("NEW BEARER: " + bearerToken);
  } else {
    Serial.println("Token error response:");
    Serial.println(https.getString());
  }

  https.end();
}

// -------------------- SENSOR --------------------
float readMQ135() {
  int raw = analogRead(A0);
  return (float)raw / 1023.0;
}

// -------------------- SEND --------------------
void sendMeasurement() {
  if (bearerToken.length() == 0) {
    Serial.println("No bearer token, skip send");
    return;
  }

  HTTPClient https;

  String url = String(baseUrl) + "/api/measurement";
  https.begin(client, url);

  https.addHeader("Content-Type", "application/json");
  https.addHeader("Authorization", "Bearer " + bearerToken);

  float val = readMQ135();

  StaticJsonDocument<256> doc;
  doc["co"] = val * 10;
  doc["no2"] = val * 5;
  doc["pm25"] = val * 20;
  doc["pm10"] = val * 30;
  doc["latitude"] = 56.895919;
  doc["longitude"] = 60.752721;

  String body;
  serializeJson(doc, body);

  int httpCode = https.POST(body);

  Serial.println("Send code: " + String(httpCode));

  if (httpCode > 0) {
    Serial.println(https.getString());
  }


  https.end();
}

// -------------------- SETUP --------------------
void setup() {
  Serial.begin(115200);

  client.setInsecure();

  connectWiFi();

  registerUser();
  delay(2000);

  getToken();

  lastTokenTime = millis();
  lastSendTime = millis();
  lastStatusTime = millis();
}

// -------------------- LOOP --------------------
void loop() {
  unsigned long now = millis();

  if (now - lastStatusTime >= statusInterval) {
    printStatus();
    lastStatusTime = now;
  }

  if (now - lastTokenTime >= tokenInterval) {
    getToken();
    lastTokenTime = now;
  }

  if (now - lastSendTime >= sendInterval) {
    sendMeasurement();
    lastSendTime = now;
  }
}
