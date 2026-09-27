#!/usr/bin/env bash
# Тестування MAUI-Android на емуляторі з мінімум ресурсів:
# - RAM 1 GB замість стандартних 2-4 GB
# - Без GPU (host-gpu вимкнено), лише SwiftShader
# - Без аудіо
# - Без передньої камери
# - Холодний старт (швидше)
#
# Передбачає, що встановлені:
#   - Android SDK (cmdline-tools, emulator, platform-tools)
#   - .NET 10 SDK + maui-android workload
#   - system image: e.g. "system-images;android-30;google_apis;x86_64"
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

AVD_NAME="lowres_test"
SYSTEM_IMG="system-images;android-30;google_apis;x86_64"
SDK_DIR="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-$HOME/Android/Sdk}}"
EMULATOR="$SDK_DIR/emulator/emulator"
AVD_MANAGER="$SDK_DIR/cmdline-tools/latest/bin/avdmanager"
SDKMANAGER="$SDK_DIR/cmdline-tools/latest/bin/sdkmanager"

echo "[1/5] Встановлюю системний образ ($SYSTEM_IMG)…"
yes | "$SDKMANAGER" "$SYSTEM_IMG" >/dev/null 2>&1 || true

echo "[2/5] Створюю AVD '$AVD_NAME' (256 MB RAM, 512 MB storage)…"
# Видаляємо старий, якщо є
"$AVD_MANAGER" delete avd -n "$AVD_NAME" 2>/dev/null || true
echo "no" | "$AVD_MANAGER" create avd -n "$AVD_NAME" -k "$SYSTEM_IMG" -d "Nexus 5" --force

# Налаштовуємо low-res config
AVD_DIR="$HOME/.android/avd/$AVD_NAME.avd"
cat >> "$AVD_DIR/config.ini" <<'EOF'
hw.ramSize=1024
hw.gpu.enabled=no
hw.gpu.mode=off
hw.audioInput=no
hw.camera.back=none
hw.camera.front=none
disk.dataPartition.size=512M
EOF

echo "[3/5] Запускаю емулятор у фоновому режимі…"
# -no-window — без GUI (headless), але ми хочемо бачити UI? Для мінімуму — без вікна.
# Якщо потрібен візуальний екран — прибрати -no-window та додати -gpu swiftshader_indirect
"$EMULATOR" -avd "$AVD_NAME" -no-window -no-audio -no-boot-anim -gpu swiftshader_indirect &
EMU_PID=$!
echo "Емулятор PID: $EMU_PID"

echo "[4/5] Чекаю, пристрій з'явиться в adb…"
# Чекаємо до 3 хвилин
timeout 180 bash -c '
  while true; do
    if adb devices 2>/dev/null | grep -q "emulator-5554"; then break; fi
    sleep 5
  done
' || { echo "Емулятор не запустився"; kill $EMU_PID 2>/dev/null; exit 1; }

echo "[5/5] Запускаю MAUI-проєкт на емуляторі…"
cd "$PROJECT_DIR"
dotnet build src/TvOptimizer.App/TvOptimizer.App.csproj -c Release
dotnet android run --project src/TvOptimizer.App/TvOptimizer.App.csproj -c Release --emulator

echo "[Cleanup] Зупиняю емулятор…"
kill $EMU_PID 2>/dev/null || true
echo "Готово!"