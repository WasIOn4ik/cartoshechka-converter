# Pmx2Vrm — конвертер PMX → VRM

Headless конвертер моделей **MMD PMX** в **VRM** на чистом .NET (без Unity в рантайме).
VRM строится поверх glTF 2.0 при помощи библиотеки [SharpGLTF](https://github.com/vpenades/SharpGLTF).

## Решения

- **Движок:** чистый .NET + SharpGLTF. Unity опционально, только для валидации.
- **Версии VRM:** поддерживаются **0.x** и **1.0** (флаг CLI `--vrm-version`).
- **Тест-данные:** синтетический минимальный PMX для unit-тестов + реальная модель пользователя для E2E.

## Соответствие форматов

| | PMX | VRM |
|---|---|---|
| Тип | бинарный MMD | glTF 2.0 + JSON-расширения |
| Координаты | левосторонние, Y-up, Z-вперёд | правосторонние, Y-up, -Z-вперёд |
| Материалы | MMD toon + sphere + edge | MToon (`VRMC_materials_mtoon` / `VRM` materialProperties) |
| Скелет | произвольные кости (JP-имена) | Humanoid (фиксированный маппинг) |
| Морфы | Vertex/UV/Material/Bone/Group | Expressions поверх glTF morph targets |
| Физика | Rigid Body + Joint (Bullet) | Spring Bone (`VRMC_springBone` / `VRM.secondaryAnimation`) |
| Единицы | ~8 units = 1 м | метры |

## Структура решения

```
Pmx2Vrm.sln
├── src/Pmx2Vrm.Core/      библиотека: Pmx/ Vrm/ Conversion/ Mapping/
├── src/Pmx2Vrm.Cli/       консоль (System.CommandLine)
└── tests/Pmx2Vrm.Tests/   xUnit
```

## Конвейер

```
PMX → PmxReader → PmxModel
   → CoordinateConverter (scale, flip Z, winding)
   → SkeletonConverter + HumanoidMapper
   → MeshConverter (POSITION/NORMAL/UV/JOINTS/WEIGHTS)
   → MaterialConverter (MMD → MToon)
   → MorphConverter + ExpressionPresetMap
   → PhysicsConverter (rigid body + joint → spring bone + collider)
   → SharpGLTF + VRM extensions → .vrm (glb)
```

## Принципиальные ограничения

- Bullet-физика → Spring Bone: аппроксимация, не 1:1.
- Bone/Group/часть UV-морфов конвертируются частично.
- Sphere map MMD ≈ MToon matcap, не идентично.

## Прогресс

См. git-историю — коммиты атомарны по стадиям конвейера.
