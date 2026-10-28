# Wardrobe pack format (Phase 6 step 7)

Wardrobe packs are how new garments arrive after R1: the downloadable content packs of Phase 9 (PRD 13). A pack is a
manifest of garments. Each one either names a garment the code generates, or points to a modelled mesh in an asset
bundle. `WardrobePacks.Validate` enforces the rules below before a pack is published or loaded.

## Manifest

```json
{
  "id": "festive_two",
  "version": 1,
  "title": "Festive two",
  "licence": "own-009",
  "garments": [
    {
      "id": "angrakha",
      "name": "Angrakha",
      "slots": ["BaseTop"],
      "covers": ["Chest", "Belly", "UpperArms", "LowerArms"],
      "occasion": ["Festive", "Wedding"],
      "inspiration": ["SouthAsian"],
      "variants": 4,
      "hidesHair": false,
      "mesh": "generator:kurta",
      "vertices": 1800,
      "culturalReview": "2026-11-02, reviewer AB"
    }
  ]
}
```

| Field | Rule |
|---|---|
| `id` (pack and garment) | Lowercase letters, digits and underscores, at most 40 characters. Garment ids are new: not already in the catalogue or another pack |
| `licence` | A row id in [LICENCE_LOG.md](../LICENCE_LOG.md) |
| `slots` | `BaseTop`, `BaseBottom`, `Mid`, `Outer`, `Headwear`, `Footwear`, `Accessory`. Robes list `BaseTop` and `BaseBottom`; a sari lists `BaseBottom` and `Mid` |
| `covers` | `Chest`, `Belly`, `UpperArms`, `LowerArms`, `Hips`, `UpperLegs`, `LowerLegs`, `Feet`, `Hair`, `Neck`. List only regions the fabric really covers: skin there is hidden, so a wrong entry leaves a see-through gap |
| `occasion` | One or more of `Everyday`, `Festive`, `Formal`, `Wedding`, `Street` |
| `inspiration` | One or more of `SouthAsian`, `Nepali`, `WestAsian`, `Western` |
| Names | No gender or audience words ("men", "women", "unisex", "only" …): every garment is open to every body (PRD 8) |
| `mesh` | `generator:<id>` for a procedural garment, or `asset:<bundle path>` for a modelled one |
| `vertices` | 3,000 or fewer per garment (the low-tier budget) |
| `variants` | 1 to 8 colour or draping variants |
| `culturalReview` | Who reviewed it and when. Required, because PRD 8 asks for review of every cultural item |

**Before a pack ships:** its garments go through the clipping check ([CLIPPING.md](CLIPPING.md)) with zero
blockers. Modelled garments (`asset:`) must be skinned to the shared skeleton's bone names, so they work with the same
poses.

The built-in wardrobe converts to this format with `WardrobePacks.FromCatalog`, and a test checks that the round trip
loses nothing.
