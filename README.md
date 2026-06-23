# Pmx2Vrm Pmx to vmr converter

Headless конвертер моделей **MMD PMX → VRM** на чистом .NET (без Unity в рантайме).
VRM собирается поверх glTF 2.0; поддерживаются версии **VRM 1.0** и **VRM 0.x**.

Архитектура и проектные решения — в [ARCHITECTURE.md](ARCHITECTURE.md).

## Возможности

- Парсинг PMX 2.0 / 2.1 (вершины, материалы, кости, морфы, физика, дисплей-фреймы).
- Геометрия: общий буфер вершин, примитивы по материалам, скиннинг (BDEF1/2/4, SDEF, QDEF).
- Скелет → glTF nodes + skin; стандартные имена костей MMD → VRM **humanoid**.
- Материалы → **MToon** (toon ramp, sphere map, edge → outline) + PBR-фоллбэк.
- Морфы → glTF morph targets + VRM **expressions** (пресеты あ/い/まばたき/笑い…).
- Физика: rigid body + joint → VRM **spring bone** + коллайдеры (аппроксимация).
- Координаты: левосторонняя MMD → правосторонняя glTF (flip Z, масштаб, winding).

## Сборка и тесты

```powershell
dotnet build
dotnet test            # 62 unit/E2E теста
```

## Использование (CLI)

```powershell
dotnet run --project src/Pmx2Vrm.Cli -- <input.pmx> [options]
```

| Опция | Описание | По умолчанию |
|---|---|---|
| `-o, --output <path>` | путь выходного `.vrm` | рядом с входным |
| `--vrm-version <1\|0>` | версия VRM | `0` |
| `--scale <float>` | метров на единицу MMD | `0.08` |
| `--title <text>` | заголовок в VRM meta | имя файла |
| `--author <text>` | автор в VRM meta | `Unknown` |
| `--spring-colliders` | генерировать коллайдеры spring-bone | выкл. |
| `--no-colliders` | принудительно отключить все коллайдеры | — |

Пример:

```powershell
dotnet run --project src/Pmx2Vrm.Cli -- model.pmx -o model.vrm --vrm-version 1 --author "Me"
```

Текстуры рядом с `.pmx` встраиваются в `.vrm`: PNG/JPEG — как есть, прочие
форматы (BMP, TGA, GIF, …) автоматически транскодируются в PNG (ImageSharp).
Отсутствующие на диске файлы остаются ссылкой-URI с предупреждением.

## Известные ограничения

- Физика Bullet → Spring Bone — аппроксимация, не точное соответствие. Коллайдеры
  по умолчанию **не** генерируются (грубое приближение групп столкновений MMD
  вызывает дрожь ткани); включаются флагом `--spring-colliders`.
- Bone / Group / UV / Material морфы не выражаются morph target'ами и пропускаются
  (с предупреждением); конвертируются только vertex- и group/flip-морфы.
- Sphere map MMD ≈ MToon matcap, не идентично.
- BMP/TGA транскодируются в PNG при встраивании (требуется наличие файла на диске).
- Метаданные лицензии по умолчанию консервативны (только автор, без перераспространения).

## Лицензии и метаданные

Конвертер по умолчанию выставляет ограничительные права (`onlyAuthor`,
`personalNonProfit`, без перераспространения). Указывайте `--author` и при
необходимости правьте `VrmMeta` под фактическую лицензию исходной модели.
