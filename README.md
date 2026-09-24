Морской бой (Unity) — тестовое задание

Минимальная онлайн-игра «Морской бой» для двух игроков в одном процессе Unity.  
Сервер — единственный источник правды; клиенты обмениваются с ним только сериализованными сообщениями через транспорт-прослойку.

Unity: 6000.3.x (Unity 6)

---

Как запустить

1. Откройте проект в Unity 6.
2. Откройте сцену `Assets/Scenes/SampleScene.unity` (или любую — bootstrap поднимается автоматически).
3. Нажмите Play.

Дополнительная настройка не нужна: UI собирается в runtime, два клиента и сервер стартуют в одной сцене.

Конфиг по умолчанию: `Assets/Config/GameConfig.asset`  
(размер поля, корабли, latency, таймер хода, retry).

EditMode-тесты: Window → General → Test Runner → EditMode → Run All  
(сборка `NavalBattle.EditModeTests`).

---

Архитектура

- `GameBootstrap` поднимает in-process сервер, транспорт и два клиента.
- `InProcessTransportHub` — очередь сообщений с задержкой S→C, drop и disconnect.
- `GameServer` хранит доски, проверяет ходы, считает miss/hit/sunk, таймер, snapshot.
- `GameClient` держит только view-state игрока (своё поле + туман войны) и pending-выстрел.
- Сообщения — JSON (`JsonUtility`) внутри `NetworkEnvelope` (`type`, `seq`, `payload`).
- У клиента нет ссылок на объекты сервера; правды о чужих кораблях клиент не получает.
- Параметры игры и сети по умолчанию задаются в `GameConfig` (ScriptableObject).

Папки: `Assets/Scripts/{Config,Shared,Messages,Transport,Server,Client,UI,Bootstrap}`.

---

Сообщения клиент ↔ сервер

Client → Server

| Тип | Назначение |
|-----|------------|
| `JoinRequest` | Войти в матч (preferred player id) |
| `FireRequest` | Выстрел (`requestId`, x, y) |
| `ReconnectRequest` | Переподключение с привязкой к player id |
| `SyncRequest` | Запросить актуальный `StateSnapshot` |

Server → Client

| Тип | Назначение |
|-----|------------|
| `Welcome` | Назначенный `playerId`, фаза |
| `MatchStarted` | Своё поле (корабли), чей ход, таймер |
| `FireRejected` | Отклонение (`NotYourTurn`, `AlreadyShot`, …) |
| `ShotResult` | Результат выстрела, следующий ход, победа |
| `StateSnapshot` | Полное разрешённое состояние для reconnect/sync |
| `OpponentConnectionChanged` | Оппонент offline/online |
| `MatchFinished` | Победитель |
| `TurnUpdate` | Смена/пауза/резюм хода и таймера |
| `Error` | Текстовая ошибка |

---

Разрыв соединения

1. Disconnect — in-flight сообщения этого peer теряются; клиент получает событие разрыва.
2. Матч переходит в `PausedDisconnected`; таймер хода замораживается.
3. Connect — peer снова в сети; клиент шлёт `ReconnectRequest` / получает `StateSnapshot`.
4. Когда оба online — фаза снова `Playing`, таймер продолжается с того же остатка (`TurnUpdate` обоим).

Пока игрок не вернулся: пауза без автопоражения (`DisconnectForfeitSeconds = 0`, forfeit не реализован).

---

Debug-панель (на клиента)

- Receive delay — задержка S→C только для этого клиента (асимметрия).
- Disconnect / Connect
- Drop next ↑ — потерять следующий C→S  
- Drop next ↓ — потерять следующий S→C  
- Sync — запросить snapshot  
- Общие: Restart Scene, лог сетевых сообщений вкл/выкл  

Легенда клеток: `S` корабль, `·` miss, `X` hit, `#` sunk, `?` pending.

---

Принятые решения и упрощения

| Тема | Решение |
|------|---------|
| Касание кораблей | Запрещено по ортогонали; диагональ разрешена |
| Попадание | Не даёт лишний ход |
| Первый ход | Случайно (`FirstTurnPlayerId = 0`), в тестах фиксируется |
| Игрок не вернулся | Пауза бесконечно; forfeit не сделан |
| Latency | Только на приём (S→C); C→S сразу, чтобы асимметрию было видно на двух полях |
| Таймер хода | По таймауту ход **пропускается** без выстрела; дедлайн — абсолютный `realtimeSinceStartup` (общий clock процесса) |
| Двойной выстрел / retry | Тот же `requestId`; сервер кеширует `ShotResult` и не двигает ход дважды |
| Потеря S→C у «не стрелявшего» | Auto `SyncRequest` + кнопка Sync |
| UI | Минимальный, без ассетов; сцена без ручной проводки |
| Сеть | Только in-process симуляция, без Netcode/Mirror |

Плюсы из ТЗ: EditMode-тесты правил, потеря сообщений + идемпотентный retry, таймер хода.

---

Быстрая проверка вручную

1. Обычная партия до победы.  
2. Latency 1000–2000 + Disconnect/Connect.  
3. Drop ↑ / Drop ↓ и retry.  
4. Ждать таймаут хода без выстрела.  
5. Restart Scene при сообщениях в пути — без ошибок в Console.
