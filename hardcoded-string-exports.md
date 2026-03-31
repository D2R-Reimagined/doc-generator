### Hardcoded String Exports — Reference Table

All user-facing / exported strings that are hardcoded rather than sourced from `.tbl` / `.json` translation files.

---

#### `ItemStatCost.cs` — `D2TxtImporter.lib\Model\Dictionaries\ItemStatCost.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 527 | `"Random Skill Tab"`, `"Only"` | descfunc 14 — `$"+{value} Random Skill Tab ({lstValue} Only)"` |
| 550 | `"Only"` | descfunc 14 — `$"+{...} ({className} Only)"` |
| 587 | `"Min"`, `"Max Player Damage"` | descfunc 19 — `$"+{value}% Min / +{value2}% Max Player Damage"` |
| 591 | `"Player Damage"` | descfunc 19 — `$"+{value}% Player Damage"` |
| 660 | `"Random"`, `"Skill"` | descfunc 27 — `$"+{parameter} Random {skillRandName} Skill"` |
| 670 | `"Only"` | descfunc 27 — `$" ({charInfo.Class} Only)"` |
| 671 | `"to"` | descfunc 27 — `$"+{valueString} to {skillRandSkill.LocalizedName}{reqString}"` |
| 678 | `"to"` | descfunc 27 fallback — `$"+{valueString} to {skillRandSkill.LocalizedName}"` |
| 754 | `"to Required Level"` | special stat `item_levelreq` — `"+" + value + " to Required Level"` |
| 758 | `"Fade"` | special stat `fade` — `valueString = "Fade"` |
| 762 | `"Extra Blood"` | special stat `item_extrablood` — `valueString = "Extra Blood"` |
| 766 | `"PASS THROUGH, I DID, LUL"` | special stat `item_nonclassskill` (placeholder/debug) |
| 821 | `"Amazon"` | `GetClassNameForRange` — skill ID range 6–35 |
| 822 | `"Sorceress"` | `GetClassNameForRange` — skill ID range 36–65 |
| 823 | `"Necromancer"` | `GetClassNameForRange` — skill ID range 66–95 |
| 824 | `"Paladin"` | `GetClassNameForRange` — skill ID range 96–125 |
| 825 | `"Barbarian"` | `GetClassNameForRange` — skill ID range 126–155 |
| 826 | `"Druid"` | `GetClassNameForRange` — skill ID range 221–250 |
| 827 | `"Assassin"` | `GetClassNameForRange` — skill ID range 251–280 |
| 828 | `"Warlock"` | `GetClassNameForRange` — skill ID range 373–402 |
| 838 | `"TODO: Unimplemented function: ..."` | fallback return for unknown descfunc (debug/export leak) |

---

#### `Equipment.cs` — `D2TxtImporter.lib\Model\Equipment\Equipment.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 52 | `"Sorceress"`, `"Barbarian"`, `"Druid"`, `"Assassin"`, `"Necromancer"`, `"Paladin"`, `"Amazon"`, `"Warlock"` | `RequiredClass` — class name matching array |
| 74 | `"Smite Damage"` | `GetDamagePrefix` — damage type label for shields |
| 81 | `"Kick Damage"` | `GetDamagePrefix` — damage type label for boots |

---

#### `ItemTypes.cs` — `D2TxtImporter.lib\Model\Dictionaries\ItemTypes.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 77 | `"Large Charm"` | `Normalizations` — maps `"medium charm"` → `"Large Charm"` |
| 78 | `"Body Armor"` | `Normalizations` — maps `"armor"` → `"Body Armor"` |
| 79 | `"Grand Charm"` | `Normalizations` — maps `"large charm"` → `"Grand Charm"` |
| 80 | `"Helm"` | `Normalizations` — maps `"merc equip"` → `"Helm"` |
| 81 | `"Hand to Hand"` | `Normalizations` — maps `"hand to hand 2"` → `"Hand to Hand"` |

---

#### `CubeQualifiers.cs` — `D2TxtImporter.lib\Model\Items\CubeQualifiers.cs`

**Input Display Names (lines 71–90):**

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 71 | `"Quantity"` | Input display for `qty=#` |
| 72 | `"Low Quality"` | Input display for `low` |
| 73 | `"Normal Quality"` | Input display for `nor` |
| 74 | `"High Quality (Superior)"` | Input display for `hiq` |
| 75 | `"Magic Item"` | Input display for `mag` |
| 76 | `"Set Item"` | Input display for `set` |
| 77 | `"Rare Item"` | Input display for `rar` |
| 78 | `"Unique Item"` | Input display for `uni` |
| 79 | `"Crafted Item"` | Input display for `crf` |
| 80 | `"Tempered Item"` | Input display for `tmp` |
| 81 | `"No Sockets"` | Input display for `nos` |
| 82 | `"Item with Sockets (#)"` | Input display for `sock=#` |
| 83 | `"Item with Sockets"` | Input display for `sock` |
| 84 | `"Not Ethereal"` | Input display for `noe` |
| 85 | `"Ethereal"` | Input display for `eth` |
| 86 | `"Upgradeable"` | Input display for `upg` |
| 87 | `"Basic Item"` | Input display for `bas` |
| 88 | `"Exceptional Item"` | Input display for `exc` |
| 89 | `"Elite Item"` | Input display for `eli` |
| 90 | `"Not a Runeword"` | Input display for `nru` |

**Output Display Names (lines 93–123):**

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 93 | `"Cow Portal"` | Output display for `Cow Portal` |
| 94 | `"Pandemonium Portal"` | Output display for `Pandemonium Portal` |
| 95 | `"Pandemonium Finale Portal"` | Output display for `Pandemonium Finale Portal` |
| 96 | `"Red Portal"` | Output display for `Red Portal` |
| 97 | `"Use Type of Input 1"` | Output display for `usetype` |
| 98 | `"Use Item from Input 1"` | Output display for `useitem` |
| 99 | `"Quantity"` | Output display for `qty=#` |
| 100 | `"Force Prefix (#)"` | Output display for `pre=#` |
| 101 | `"Force Suffix (#)"` | Output display for `suf=#` |
| 102 | `"Low Quality Item"` | Output display for `low` |
| 103 | `"Normal Item"` | Output display for `nor` |
| 104 | `"High Quality Item (Superior)"` | Output display for `hiq` |
| 105 | `"Magic Item"` | Output display for `mag` |
| 106 | `"Set Item"` | Output display for `set` |
| 107 | `"Rare Item"` | Output display for `rar` |
| 108 | `"Unique Item"` | Output display for `uni` |
| 109 | `"Crafted Item"` | Output display for `crf` |
| 110 | `"Tempered Item"` | Output display for `tmp` |
| 111 | `"Ethereal Item"` | Output display for `eth` |
| 112 | `"Item with Sockets"` | Output display for `sock` |
| 113 | `"Item with Sockets (#)"` | Output display for `sock=#` |
| 114 | `"Keep Modifiers"` | Output display for `mod` |
| 115 | `"Unsocket (Destroy Socketed)"` | Output display for `uns` |
| 116 | `"Remove Socketed (Return)"` | Output display for `rem` |
| 117 | `"Regenerate Unique (Reroll if Base Upgraded)"` | Output display for `reg` |
| 118 | `"Exceptional Item"` | Output display for `exc` |
| 119 | `"Elite Item"` | Output display for `eli` |
| 120 | `"Repair Item"` | Output display for `rep` |
| 121 | `"Recharge Charges"` | Output display for `rch` |
| 122 | `"Set Level (#)"` | Output display for `lvl=#` |

---

#### `CubeRecipeV2.cs` — `D2TxtImporter.lib\Model\Items\CubeRecipeV2.cs`

**AddNote Recipe Labels (lines 430–685):**

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 430 | `"Orb of Corruption Recipe"` | AddNote — recipe classification |
| 436 | `"Orb of Conversion Recipe"` | AddNote — recipe classification |
| 442 | `"Orb of Assemblage Recipe"` | AddNote — recipe classification |
| 448 | `"Orb of Infusion Recipe"` | AddNote — recipe classification |
| 454 | `"Orb of Socketing Recipe"` | AddNote — recipe classification |
| 460 | `"Socket Punch Recipe"` | AddNote — recipe classification |
| 466 | `"Orb of Shadows Recipe"` | AddNote — recipe classification |
| 472 | `"Pliers Recipe"` | AddNote — recipe classification |
| 478 | `"Item Crafting Recipe"` | AddNote — recipe classification |
| 484 | `"Item Crafting Recipe"` | AddNote — recipe classification (2nd path) |
| 490 | `"Item Crafting Recipe"` | AddNote — recipe classification (3rd path) |
| 498 | `"Sunder Item Crafting Recipe"` | AddNote — recipe classification |
| 506 | `"Sunder Item Crafting Recipe"` | AddNote — recipe classification (latent) |
| 512 | `"Tristram Uber Souls Recipe"` | AddNote — recipe classification |
| 518 | `"Key Conversion Recipe"` | AddNote — recipe classification |
| 530 | `"Rejuvenation Potion Recipe"` | AddNote — recipe classification |
| 536 | `"Force Ethereal Recipe"` | AddNote — recipe classification |
| 543 | `"Repair Non-Ethereal Item"` | AddNote — recipe classification |
| 555 | `"Force White Recipe"` | AddNote — recipe classification |
| 567 | `"Reroll Item Recipe"` | AddNote — recipe classification |
| 580 | `"Reroll Item Recipe"` | AddNote — recipe classification (2nd path) |
| 591 | `"Jewel Upgrade Recipe"` | AddNote — recipe classification |
| 603 | `"Recycle Recipe"` | AddNote — recipe classification |
| 613 | `"Splash Charm Upgrade Recipe"` | AddNote — recipe classification |
| 619 | `"Uber Charm Upgrade Recipe"` | AddNote — recipe classification |
| 624 | `"Uber Charm Upgrade Recipe"` | AddNote — recipe classification (2nd path) |
| 630 | `"Uber Charm Upgrade Recipe"` | AddNote — recipe classification (3rd path) |
| 636 | `"Base Tier Upgrade Recipe"` | AddNote — recipe classification |
| 642 | `"Repair Ethereal Recipe"` | AddNote — recipe classification |
| 648 | `"Replenish Quiver/Bolt Case Recipe"` | AddNote — recipe classification |
| 654 | `"Cow Portal Recipe"` | AddNote — recipe classification |
| 660 | `"Random Mini-Uber Portal Recipe"` | AddNote — recipe classification |
| 666 | `"Tristram Uber Portal Recipe"` | AddNote — recipe classification |
| 672 | `"Item Enchantment Recipe"` | AddNote — recipe classification |
| 678 | `"Recycle Recipe"` | AddNote — recipe classification (2nd path) |
| 685 | `"Reroll Uber Ancient Material"` | AddNote — recipe classification |

**Sunder Item Name Checks (lines 495–504):**

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 495–496 | `"Cold Rupture"`, `"Flame Rift"`, `"Crack of the Heavens"`, `"Rotting Fissure"`, `"Bone Break"`, `"Black Cleft"` | Sunder charm name match |
| 503–504 | `"Latent Cold Rupture"`, `"Latent Flame Rift"`, `"Latent Crack of the Heavens"`, `"Latent Rotting Fissure"`, `"Latent Bone Break"`, `"Latent Black Cleft"` | Latent sunder charm name match |

**Special Output Main Tokens (lines 953–956):**

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 953 | `"Cow Portal"` | `_specialOutputMains` set |
| 954 | `"Pandemonium Portal"` | `_specialOutputMains` set |
| 955 | `"Pandemonium Finale Portal"` | `_specialOutputMains` set |
| 956 | `"Red Portal"` | `_specialOutputMains` set |

**Other Export Strings:**

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 600 | `"Tome of Identify"` | Input name match for recycle recipe detection |
| 883 | `"Keep Modifiers"` | Output qualifier `mod` fallback return |
| 1019 | `"Return Gem Bag, Update Gem Credits"`, `"Return Base Type"` | `usetype` special output name |
| 1051 | `"Return Gem Bag, Update Gem Credits"`, `"Return Updated Item"` | `useitem` special output name |
| 1097 | `"Any Item"` | Fallback when no specific item type resolved |
| 1177 | `"Any Item"` | Fallback when no specific item type resolved (second path) |

---

#### `Unique.cs` — `D2TxtImporter.lib\Model\Items\Unique.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 43 | `"Cold Rupture"`, `"Flame Rift"`, `"Crack of the Heavens"`, `"Rotting Fissure"`, `"Bone Break"`, `"Black Cleft"` | `sunderNames` — forced Vanilla flag for sunder charms |
| 455 | `"Unhandled Damage Prefix"` | Fallback when `GetDamagePrefix` returns null |
| 564–574 | `"amulet of the viper"`, `"staff of kings"`, `"horadric staff"`, `"hell forge hammer"`, `"khalimflail"`, `"superkhalimflail"`, `"pliers"`, `"grabber"`, `"gem bag"`, `"Keychain"`, `"rainbow facet"` | `ItemsToIgnore` — items excluded from unique export |

---

#### `AutoMagic.cs` — `D2TxtImporter.lib\Model\Dictionaries\AutoMagic.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 23 | `"Automagic"` | `PType` property — exported type label |

---

#### `MagicPrefix.cs` — `D2TxtImporter.lib\Model\Dictionaries\MagicPrefix.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 25 | `"Prefix"` | `PType` property — exported type label |

---

#### `MagicSuffix.cs` — `D2TxtImporter.lib\Model\Dictionaries\MagicSuffix.cs`

| Row | Hardcoded String | Context |
|-----|-----------------|---------|
| 25 | `"Suffix"` | `PType` property — exported type label |

---

### Summary

- **Total hardcoded export strings found: ~150+** across **10 files**.
- The largest concentrations are in `CubeQualifiers.cs` (~53 display name entries) and `CubeRecipeV2.cs` (~50 AddNote labels + special output names + portal tokens).
- `ItemStatCost.cs` has 21 entries, primarily in `descfunc` display formatting and the class-name-by-skill-range lookup.
- `ItemTypes.cs` has 5 normalization mappings, `Equipment.cs` has 10 (8 class names + 2 damage labels).
- `Unique.cs` has sunder names, ignore list, and a damage prefix fallback.
- The remaining files (`AutoMagic.cs`, `MagicPrefix.cs`, `MagicSuffix.cs`) each have 1 type label string.
