# 📺 Android TV Optimizer — Phone App

Керуй будь-яким Android TV / Google TV **прямо з телефона** по Wi-Fi (Wireless Debugging, Android 11+).
Без root, без ПК. Прийшов до будь-кого → підключив → аудит → почистив → прискорив анімації → поставив Projectivy HOME.

> Конфіги моделей + community-списки живуть в сусідньому репозиторії
> [`android-tv-optimizer`](https://github.com/) і підтягуються свіжими по HTTP.
> Цей APK — тільки тонкий клієнт: `Скачати конфіг → fingerprint ТВ → audit → tweaks → apply`.

![Platform](https://img.shields.io/badge/Platform-Android_8%2B-green)
![License](https://img.shields.io/badge/License-MIT-orange)

## Фічі (MVP)

1. **Connect** — Wireless Debugging pairing (код + порт пейрингу → ADB-порт), mDNS-підказка, збереження останнього ТВ.
2. **Audit** — порт `scripts/audit.sh`: 🟢 SAFE-TO-CLEAN / 🟡 REVIEW (curated community) / 🔍 HEURISTIC / 🔴 PROTECTED / ❓ UNIDENTIFIED. Read-only за замовчуванням.
3. **Debloat apply** — тільки TIER_1 кнопка + backup-список + Rollback (`install-existing`). UNIDENTIFIED ніколи не має кнопки «видалити все».
4. **Tweaks** (нове!): швидкість анімацій, фонові процеси, doze, screensaver, HDR/роздільна здатність інфо — все через безпечні `settings put global/system` + `pm`. Жодних `disable` на відео-стек.
5. **Set HOME** — Projectivy як HOME (`cmd role` + preferred, емуляція ручного чойзера) + перевірка `verify`.
6. **Offline cache** — конфіги кешуються на 7 днів, працює без інтернету в гостях.

## ADB-ядро

- **Kadb** (`com.flyfishxu:kadb`, Apache-2.0) — `Kadb.create(host,port)` + `Kadb.pair(host,pairPort,code)`, shell/push/pull/install/uninstall. Жодних російських залежностей.
- Fallback: `libadb-android` (MuntashirAkon, GPL/Apache) — згадано в `docs/APP_SPEC.md`, за замовчуванням не тягнемо щоб не роздувати APK.

## Структура

```
android-tv-optimizer-app/
├── app/src/main/java/com/optimizer/tv/
│   ├── MainActivity.kt
│   ├── adb/AdbGateway.kt        # обгортка над Kadb
│   ├── config/ConfigRepo.kt     # GitHub raw + кеш + парсер .conf/.txt
│   ├── audit/AuditEngine.kt     # порт audit.sh
│   ├── tweaks/TweaksEngine.kt   # анімації/процеси/doze/інфо
│   ├── safety/Guard.kt          # NEVER_TOUCH + PROTECTED фільтр
│   └── ui/{Connect,Audit,Tweaks,Apply}Screen.kt
├── docs/{APP_SPEC,TWEAKS}.md
└── gradle/libs.versions.toml
```

## Збірка

```bash
# Потрібні JDK 17 + Android SDK 34
./gradlew assembleDebug
# APK: app/build/outputs/apk/debug/app-debug.apk
```

Без Android SDK — дивись `docs/APP_SPEC.md` (логіка повністю описана, можна рев'ювити без збірки).

## Безпека (як в основному репо)

1. Ніколи не `disable` fallback-HOME (`launcherx` лишається enabled).
2. Не чіпати відео-стек (`mitv.service`, `livetv`, `videoplayer`, `setup`).
3. `apply` тільки на TIER_1; REVIEW/UNIDENTIFIED — поштучно з підтвердженням.
4. Перед кожним apply — backup-список для `pm install-existing --user 0 <pkg>`.

## Ліцензія

[MIT](LICENSE)

## 🧪 Тестування на low‑ресурсному Android‑емуляторі

Для швидкого розробки та CI можна запустити тести на minimal‑resource AVD.

### Підготовка

1. Встановити Android SDK (emulator, platform‑tools, cmdline‑tools).  
2. Встановити .NET 10 SDK + `maui-android` workload.  
3. Завантажити system image, наприклад:  

   ```bash
   $ANDROID_SDK/cmdline-tools/latest/bin/sdkmanager "system-images;android-30;google_apis;x86_64"
   ```

### Скрипт

```bash
bash scripts/test-emulator-low.sh
```

Скрипт створює AVD з 256–1024 MB RAM, GPU вимкнено (SwiftShader), аудіо вимкнено та запускає `dotnet build … && dotnet android run …` на цьому образі.

### Що робить скрипт

- Створює (або перезабUIDES) AVD `lowres_test` на основі Google API Android 30.  
- Налаштовує `hw.ramSize=1024`, `hw.gpu.enabled=no`, `hw.audioInput=no`, `hw.camera.back=none`, `hw.camera.front=none`.  
- Запускаєemu‑безвікнічний режим (`-no-window`) щоб економити CPU/GPU.  
- Чекає, доки ADB розезнає пристрій (до 3 хв).  
- Збирає MAUI‑проєкт у режимі Release і деплоїть наemuлятор.  
- Після завершення зупиняєemuлятор.

### Використання у CI

Додати stage у GitHub Actions, який викликає `bash scripts/test-emulator-low.sh` після `dotnet build`.  
Це дозволяє перевіряти злиття PR без вимagalної облачної інфраструктури.
