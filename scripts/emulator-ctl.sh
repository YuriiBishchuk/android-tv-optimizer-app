#!/usr/bin/env bash
# =============================================================================
# emulator-ctl.sh — Керування життєвим циклом Android TV та Phone емуляторів
# =============================================================================
# Підтримує:
#   - Автономний запуск у headless режимі з апаратним KVM-прискоренням
#   - Моніторинг завантаження (sys.boot_completed=1)
#   - Роботу як на сервері всередині контейнера, так і на робочій станції
#   - Чисте завершення роботи та звільнення портів
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_DIR="/tmp/android-emulators"
mkdir -p "$PID_DIR"

ANDROID_HOME="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-/opt/android-sdk}}"
EMULATOR="$ANDROID_HOME/emulator/emulator"
ADB="$ANDROID_HOME/platform-tools/adb"
AVDMANAGER="$ANDROID_HOME/cmdline-tools/latest/bin/avdmanager"

DEFAULT_BOOT_TIMEOUT=180

resolve_avd_name() {
    local target="${1:-tv}"
    case "$target" in
        tv|android-tv|tv_1080p)
            echo "test_tv"
            ;;
        phone|mobile|pixel)
            echo "test_phone"
            ;;
        *)
            echo "$target"
            ;;
    esac
}

cmd_start() {
    local target="${1:-tv}"
    local avd_name
    avd_name=$(resolve_avd_name "$target")
    local pid_file="$PID_DIR/${avd_name}.pid"

    if [ -f "$pid_file" ]; then
        local old_pid
        old_pid=$(cat "$pid_file" 2>/dev/null || true)
        if [ -n "$old_pid" ] && kill -0 "$old_pid" 2>/dev/null; then
            echo "⚠️ Емулятор '$avd_name' вже запущений (PID: $old_pid)."
            cmd_status
            return 0
        fi
        rm -f "$pid_file"
    fi

    echo "🚀 Запуск емулятора '$avd_name' (KVM, headless)..."
    if [ ! -x "$EMULATOR" ]; then
        echo "❌ Помилка: emulator не знайдено за шляхом $EMULATOR" >&2
        exit 1
    fi

    # Очищуємо старі блокування snapshots/qcow якщо були збої
    "$EMULATOR" -avd "$avd_name" \
        -no-window \
        -no-audio \
        -no-boot-anim \
        -gpu swiftshader_indirect \
        -read-only \
        > "/tmp/emu_${avd_name}.log" 2>&1 &

    local emu_pid=$!
    echo "$emu_pid" > "$pid_file"
    echo "Емулятор запущено у фоні (PID: $emu_pid). Лог: /tmp/emu_${avd_name}.log"

    cmd_wait "$target" "$DEFAULT_BOOT_TIMEOUT"
}

cmd_wait() {
    local target="${1:-tv}"
    local avd_name
    avd_name=$(resolve_avd_name "$target")
    local timeout_secs="${2:-$DEFAULT_BOOT_TIMEOUT}"
    local pid_file="$PID_DIR/${avd_name}.pid"

    echo "⏳ Очікування готовності ADB та завершення завантаження Android ($avd_name, таймаут: ${timeout_secs}с)..."
    local start_time
    start_time=$(date +%s)

    # 1. Чекаємо появи пристрою в adb
    while true; do
        if "$ADB" devices 2>/dev/null | grep -q "emulator-"; then
            break
        fi
        local now
        now=$(date +%s)
        if [ $((now - start_time)) -ge "$timeout_secs" ]; then
            echo "❌ Таймаут: пристрій не з'явився в ADB." >&2
            cmd_stop "$target"
            return 1
        fi
        sleep 2
    done

    # 2. Чекаємо завершення завантаження системи (sys.boot_completed=1)
    while true; do
        local booted
        booted=$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r\n' || true)
        if [ "$booted" = "1" ]; then
            echo "✅ Емулятор '$avd_name' повністю завантажено і готовий до тестування!"
            break
        fi
        local now
        now=$(date +%s)
        if [ $((now - start_time)) -ge "$timeout_secs" ]; then
            echo "❌ Таймаут: sys.boot_completed не став 1 за $timeout_secs секунд." >&2
            cmd_stop "$target"
            return 1
        fi
        sleep 3
    done

    cmd_status
}

cmd_stop() {
    local target="${1:-all}"
    if [ "$target" = "all" ]; then
        for pf in "$PID_DIR"/*.pid; do
            [ -f "$pf" ] || continue
            local name
            name=$(basename "$pf" .pid)
            cmd_stop "$name"
        done
        return 0
    fi

    local avd_name
    avd_name=$(resolve_avd_name "$target")
    local pid_file="$PID_DIR/${avd_name}.pid"

    echo "🛑 Зупинка емулятора '$avd_name'..."
    # Спроба м'якої зупинки через emu kill
    "$ADB" emu kill 2>/dev/null || true

    if [ -f "$pid_file" ]; then
        local pid
        pid=$(cat "$pid_file" 2>/dev/null || true)
        if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
            sleep 1
            kill -9 "$pid" 2>/dev/null || true
        fi
        rm -f "$pid_file"
    fi

    echo "✅ Емулятор '$avd_name' зупинено."
}

cmd_status() {
    echo "📱 Статус емуляторів та ADB підключень:"
    "$ADB" devices -l || true
    for pf in "$PID_DIR"/*.pid; do
        [ -f "$pf" ] || continue
        local name
        name=$(basename "$pf" .pid)
        local pid
        pid=$(cat "$pf" 2>/dev/null || true)
        if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
            echo " - $name: RUNNING (PID $pid)"
            local model
            model=$("$ADB" shell getprop ro.product.model 2>/dev/null | tr -d '\r\n' || true)
            local version
            version=$("$ADB" shell getprop ro.build.version.release 2>/dev/null | tr -d '\r\n' || true)
            if [ -n "$model" ]; then
                echo "   Модель: $model | Android: $version"
            fi
        else
            echo " - $name: STOPPED (stale pidfile)"
            rm -f "$pf"
        fi
    done
}

case "${1:-status}" in
    start)
        cmd_start "${2:-tv}"
        ;;
    wait)
        cmd_wait "${2:-tv}" "${3:-$DEFAULT_BOOT_TIMEOUT}"
        ;;
    stop)
        cmd_stop "${2:-all}"
        ;;
    status)
        cmd_status
        ;;
    restart)
        cmd_stop "${2:-tv}"
        cmd_start "${2:-tv}"
        ;;
    *)
        echo "Використання: $0 {start|stop|wait|status|restart} [tv|phone] [timeout_secs]"
        exit 1
        ;;
esac
