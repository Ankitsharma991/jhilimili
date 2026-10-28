# Phase 6 plan: avatar and wardrobe (R1)

PRD section 19, Phase 6, features F15 (avatar, preset and manual) and F16 (wardrobe v1). PRD 8 has the design rules,
PRD 12 the creator screen and PRD 13.5 `profile.json`. Started 2026-10-28, ahead of R0 data, by the owner's decision.
The PRD says Phase 6 onwards waits for R0 data, so expect R0 results to change priorities here.

## Definition of done (from the PRD)

- **F15:** create, save and load an avatar; it fits all clothing.
- **F16:** layered slots; no clipping on 3 body shapes.
- **Deliverables:** an avatar creator, at least 20 garments across 5 style tags, and a clipping test report.
- **Exit gate:** users keep the avatar in a meaningful share of sessions; no clipping blockers.

## Decision: a procedural, stylised character

The PRD allows "choose or commission a base humanoid rig". This phase builds the character in code, the way the
crackers, sky and store art were built:
- one shared skeleton with Unity humanoid bone names;
- a stylised low-poly body with 3 body-shape blend shapes;
- garments generated as shells over the same body surface, skinned to the same bones and carrying the same
  body-shape blend shapes, so they follow every body and pose;
- skin masks hide the body under a garment.

Why:
- the art is our own (no licence rows);
- it works offline and stays light on budget phones (one skinned mesh per layer, a few hundred triangles each);
- it can't clip by construction, which the automated test in step 7 checks.

What it costs: the look is stylised rather than realistic, which the PRD accepts ("Controlled, stylised"). Because the
data model and creator are independent of the mesh source, a commissioned rig can replace the generator later.

## Rules from PRD 8

- Body style is never tied to clothing or hair; every item is available to every body.
- Nothing is locked by gender or region. Garments are tagged by **occasion** and **inspiration**, and none is
  presented as a single national dress.
- The optional name stays on the phone. It is never sent to analytics.
- Customisation: body style and proportions, continuous skin tone, face shape, eyes, nose, mouth, brows, hairstyles
  and colours (including head coverings), facial hair, height presets, glasses, jewellery, accessories, animation
  style, optional name.

## Build order

| # | Step | Main pieces | Tests |
|---|------|-------------|-------|
| 1 | Data model | `AvatarProfile` (every PRD 8 option), skin tone ramp, slots and layering rules, garment catalogue model (slot, occasion and inspiration tags, colour variants, the body regions it covers), presets, `profile.json` with versions and migration, validation that repairs bad saves | Every rule; save, load, migrate; no gender or region field anywhere |
| 2 | Body generator | Skeleton, stylised body mesh with 3 body-shape blend shapes and height presets, skin-tone material, face features, hair and head-covering meshes, glasses and jewellery | Mesh sizes, bone names, blend shapes present |
| 3 | Garment generator | Shell garments by region (tops, kurtas, robes, trousers, skirts, drapes, jackets, headwear, footwear), fit morphs, skin masks, colour variants. **20 or more garments across 5 tags** | Catalogue lint: every garment on every body |
| 4 | Animation | Idle, walk, ignite gesture, celebrate, on the shared skeleton; animation style (calm or lively) | Clips exist, loop, stay within joint limits |
| 5 | Avatar creator | PRD 12 screen: rotating preview; Body, Face, Hair and Outfit tabs; presets; **Skip always visible**; save | Play Mode: create, save, reload; Skip |
| 6 | Courtyard | The avatar stands by the play area; ignite gesture when you light, celebrates on a payoff; tier rules (fewer bones and rigid skinning on low tier) | Play Mode smoke; GC guard still passes |
| 7 | Clipping matrix and packs | Automated check of every garment × body shape × animation pose, with a report; wardrobe pack manifest format (feeds Phase 9) | The report is the deliverable |
| 8 | Measure | Analytics events (`avatar_saved`, `outfit_changed`, without the name), performance on the device tiers, docs | Schema and PII guard |

## Status

| Step | Status |
|---|---|
| 1 Data model | Done 2026-10-28. `AvatarProfile` with every PRD 8 option (no gender or region field, enforced by a test); continuous skin-tone ramp (11 anchors); 6 PRD slots, with base split into top and bottom; **38 garments** tagged by 5 occasions (Everyday, Festive, Formal, Wedding, Street) and 4 inspirations (South Asian, Nepali, West Asian, Western); layering rules (full-length robes take top and bottom, a sari takes the bottom and the drape, up to 3 accessories); 12 diverse presets; `profile.json` with migration and repair. 21 tests |
| 2 Body generator | Done 2026-10-28. 19-bone humanoid skeleton (Unity humanoid names); lofted body of **634 vertices** with 6 blend shapes (slim, broad, shoulders and hips wide or narrow); heights 92/100/108%, leg length and head size on bones, stylised head (×1.15); 5 face shapes, 4 eye shapes and 6 eye colours, noses, mouths, brows, ears, 10 hairstyles with natural hairlines, 4 facial hair styles, 3 glasses; at most about 2,200 head vertices; skin mask hides only skin under drawn garments. Review renders: **Utsav → Phase 6 → Capture Avatar Shots**. 11 tests |
| 3 Garment generator | Done 2026-10-28. All **38 garments** generated from the body's rings with layer gaps (base 8 mm, mid 16 mm, outer 26 mm), carrying the body's 6 blend shapes as fit morphs; flared skirts and hems sized from the same body factors so they clear broad legs; drapes (sari pallu in two styles, dupatta, shawl ends) as ribbons; headwear and earrings on the Head bone; colour variants with trims. Covered regions corrected where geometry is open (saris, shawl, chappals, dhoti). Review sheets: **Capture Avatar Shots** writes `avatars-wardrobe-1..4.png`. 6 tests |
| 4 Animation | Done 2026-10-28. Procedural clips on the shared skeleton: idle (breathing, weight shift, look around; seamless 8 s loop), walk (in place; 1.05 s loop), ignite (crouch, lean, right hand to the fuse, stand; the hips drop by the legs' shortening so the feet stay down), celebrate (arms up and a hop; lively hops twice). Calm or lively style. `AvatarAnimator`: 0.25 s crossfades, one-shots return to idle or walk, no per-frame allocation. Long hems (skirts, saris, robes, kurtas) follow the thighs. Found by the pose renders: short and three-quarter sleeves left bare gaps under the skin mask (fixed). 9 tests. Open for step 7: a deep crouch stretches long skirts |
