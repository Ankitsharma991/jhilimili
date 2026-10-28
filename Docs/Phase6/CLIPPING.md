# Clipping test report (Phase 6 step 7)

Generated 2026-10-28 by `ClippingCheck.RunMatrix` (Utsav → Phase 6 → Write Clipping Report).

**570 combinations:** 38 garments × 3 body styles (slim, average, broad) × 5 poses (idle, walk (left leg forward), walk (right leg forward), ignite crouch, celebrate).

**Blockers: 0.** Warnings: 87. A blocker is penetration over 1.5 cm or stretch over 2.2×; a warning is over 0.6 cm or 1.6×.

What is checked: garments built on the body's own rings with the same bone weights cannot clip by construction (tops, sleeves, trousers, shoes). The check covers where weights differ: long hems that follow the thighs (uncovered legs and feet, trousers, shoes and inner hems must stay inside), drapes pinned to single bones (they must stay outside the torso under them), and hem stretch. Skin under a garment is hidden by the skin mask, so masked body parts are not counted.

## Worst 15

| Garment | Outfit | Body | Pose | Worst | Stretch | |
|---|---|---|---|---|---|---|
| sherwani | sherwani + sneakers + churidar + kurta | slim | ignite crouch | hem 1.2 cm | 1.59× | ⚠ |
| salwar | salwar + sneakers + kurta | broad | walk (left leg forward) | hem 1.2 cm | 1.25× | ⚠ |
| salwar | salwar + sneakers + kurta | broad | walk (right leg forward) | hem 1.2 cm | 1.25× | ⚠ |
| shorts | shorts + sneakers + kurta | broad | walk (left leg forward) | hem 1.2 cm | 1.25× | ⚠ |
| shorts | shorts + sneakers + kurta | broad | walk (right leg forward) | hem 1.2 cm | 1.25× | ⚠ |
| sherwani | sherwani + sneakers + churidar + kurta | average | ignite crouch | hem 0.8 cm | 1.64× | ⚠ |
| salwar | salwar + sneakers + kurta | average | walk (left leg forward) | hem 1.0 cm | 1.22× | ⚠ |
| salwar | salwar + sneakers + kurta | average | walk (right leg forward) | hem 1.0 cm | 1.22× | ⚠ |
| shorts | shorts + sneakers + kurta | average | walk (left leg forward) | hem 1.0 cm | 1.22× | ⚠ |
| shorts | shorts + sneakers + kurta | average | walk (right leg forward) | hem 1.0 cm | 1.22× | ⚠ |
| salwar | salwar + sneakers + kurta | slim | walk (left leg forward) | hem 0.9 cm | 1.20× | ⚠ |
| salwar | salwar + sneakers + kurta | slim | walk (right leg forward) | hem 0.9 cm | 1.20× | ⚠ |
| shorts | shorts + sneakers + kurta | slim | walk (left leg forward) | hem 0.9 cm | 1.20× | ⚠ |
| shorts | shorts + sneakers + kurta | slim | walk (right leg forward) | hem 0.9 cm | 1.20× | ⚠ |
| trousers | trousers + sneakers + kurta | broad | walk (left leg forward) | hem 0.9 cm | 1.25× | ⚠ |

## Every garment (worst case across bodies and poses)

| Garment | Worst | Stretch | |
|---|---|---|---|
| tshirt | none | 1.00× | ✅ |
| shirt | none | 1.00× | ✅ |
| kurta | none | 1.76× | ⚠ warning |
| short_kurta | none | 1.22× | ✅ |
| choli | none | 1.00× | ✅ |
| daura | none | 1.70× | ⚠ warning |
| jeans | hem 0.2 cm | 1.76× | ⚠ warning |
| trousers | hem 0.9 cm | 1.76× | ⚠ warning |
| churidar | none | 1.76× | ⚠ warning |
| salwar | hem 1.2 cm | 1.76× | ⚠ warning |
| suruwal | hem 0.9 cm | 1.76× | ⚠ warning |
| dhoti | hem 0.9 cm | 1.76× | ⚠ warning |
| lehenga | none | 1.61× | ⚠ warning |
| long_skirt | none | 1.54× | ✅ |
| shorts | hem 1.2 cm | 1.76× | ⚠ warning |
| sari_nivi | none | 1.52× | ✅ |
| sari_seedha | none | 1.52× | ✅ |
| abaya | none | 1.59× | ✅ |
| thobe | none | 1.56× | ✅ |
| nehru_jacket | none | 1.76× | ⚠ warning |
| hoodie | none | 1.76× | ⚠ warning |
| dupatta | none | 1.76× | ⚠ warning |
| blazer | none | 1.76× | ⚠ warning |
| sherwani | hem 1.2 cm | 1.78× | ⚠ warning |
| shawl | none | 1.76× | ⚠ warning |
| dhaka_topi | none | 1.76× | ⚠ warning |
| turban | none | 1.76× | ⚠ warning |
| hijab | none | 1.76× | ⚠ warning |
| ghutra | none | 1.76× | ⚠ warning |
| cap | none | 1.76× | ⚠ warning |
| sneakers | none | 1.76× | ⚠ warning |
| juttis | none | 1.76× | ⚠ warning |
| chappals | none | 1.76× | ⚠ warning |
| formal_shoes | none | 1.76× | ⚠ warning |
| bangles | none | 1.76× | ⚠ warning |
| necklace | none | 1.76× | ⚠ warning |
| earrings | none | 1.76× | ⚠ warning |
| watch | none | 1.76× | ⚠ warning |
