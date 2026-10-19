# Ordinary building and zone catalog coverage — Stellaris 4.5.2

Checked against the local game source set; definitions: **644**, Included 194, ExcludedSpecific 450, Unresolved 0. There are no duplicate IDs. Each definition lists its relative path, source file SHA-256, and line. Full game script bodies are not retained.

Statuses: Included means ordinary organic non-gestalt content; technology, upgrade, placement, zone, ordinary civic, and ethics eligibility remain in the catalog. ExcludedSpecific applies to event, fallen empire, branch office/holding, primitive/origin, hive/machine, mobile, wilderness, habitat, ringworld/arcology/nexus, arkship, and other unique scenario-branch files/conditions. Upgrade tiers available only through an excluded chain receive the same exclusion transitively; upgraded ordinary tiers remain Included. Special terms within an ordinary definition are retained as conditional terms and are not mixed into ordinary active flows. Unresolved is used for ambiguous source shapes; unknown is not treated as false or zero.

## Summary

| Type | Included | ExcludedSpecific | Unresolved |
|---|---:|---:|---:|
| Building | 165 | 333 | 0 |
| Zone | 29 | 117 | 0 |

## Full discovered inventory

| Type | ID | Status | Exclusion basis | Source | Line | SHA-256 |
|---|---|---|---|---|---:|---|
| Building | `aesthetic_wonders_holomuseum` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/21_grand_archive_buildings.txt` | 132 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `building_abandoned_gene_clinic` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 1381 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_adakkaria_patriotic_institute` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/19_cosmic_storm_buildings.txt` | 578 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_advanced_storm_attraction_center` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 130 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_advanced_storm_repellent_center` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 305 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_advanced_storm_resistant_production` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 466 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_affluence_center` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 695 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_affluence_emporium` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 543 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_ai_emporium` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 4231 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_akx_worm_3` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 43 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_alpha_hub` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 3347 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_amphitheater_of_the_mind` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 269 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_amusement_megaplex` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1204 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_ancient_control_center` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 10 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_ancient_cryo_chamber` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 497 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_ancient_hive_capital` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 65 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_ancient_palace` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 129 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_ancient_ward_1` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 807 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_ancient_ward_2` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 844 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_archaeo_refinery` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1214 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_archaeostudies_faculty` | Included | — | `common/buildings/05_research_buildings.txt` | 653 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_artist_patron` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 118 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_astral_siphon_1` | ExcludedSpecific | astral_scenario_definition_file | `common/buildings/18_astral_planes_buildings.txt` | 5 | `287d82d0dff2935e5699c817fd224655fb7c7580fa16eceb44f5c91bd063980e` |
| Building | `building_astral_siphon_2` | ExcludedSpecific | astral_scenario_definition_file | `common/buildings/18_astral_planes_buildings.txt` | 94 | `287d82d0dff2935e5699c817fd224655fb7c7580fa16eceb44f5c91bd063980e` |
| Building | `building_astral_siphon_3` | ExcludedSpecific | astral_scenario_definition_file | `common/buildings/18_astral_planes_buildings.txt` | 183 | `287d82d0dff2935e5699c817fd224655fb7c7580fa16eceb44f5c91bd063980e` |
| Building | `building_astrometeorology_observation_center` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 659 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_augmentation_bazaars` | Included | — | `common/buildings/20_machine_age_buildings.txt` | 5 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_augmentation_bazaars_branch` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 4085 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_augmentation_center` | Included | — | `common/buildings/20_machine_age_buildings.txt` | 2005 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_autochthon_monument` | Included | — | `common/buildings/08_unity_buildings.txt` | 6 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_autocurating_vault` | Included | — | `common/buildings/08_unity_buildings.txt` | 2827 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_automation_1` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 2003 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_2` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 2056 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_farmer_1` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1842 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_farmer_2` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1914 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_miner_1` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1701 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_miner_2` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1773 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_technician_1` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1560 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_automation_technician_2` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1632 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_avatar_chamber_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2093 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_avatar_chamber_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2174 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_avatar_chamber_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2257 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_avatar_chamber_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2338 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_baol_organic_plant` | Included | — | `common/buildings/03_resource_buildings.txt` | 515 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_battlement_of_steel` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 490 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_betharian_power_plant` | ExcludedSpecific | planetary_deposit_building_file | `common/buildings/10_deposit_buildings.txt` | 143 | `2d05fffe2afee8140cf1414192ff324805cfa902e9d418e1b77367e5d1398096` |
| Building | `building_bio_furnace` | ExcludedSpecific | extreme_frontiers_origin_definition_file | `common/buildings/22_extreme_frontiers_buildings.txt` | 120 | `0b8aaf496361e621308ccb3a7b8df9e8a2d8d01adf2da9ecc51338afdd0fafd5` |
| Building | `building_bio_reactor` | Included | — | `common/buildings/03_resource_buildings.txt` | 1842 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_bio_reactor_2` | Included | — | `common/buildings/03_resource_buildings.txt` | 1919 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_bio_reprocessing_facilities` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1983 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_bioelectric_stimulator_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1404 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_bioelectric_stimulator_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1446 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_bioelectric_stimulator_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1491 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_bioelectric_stimulator_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1562 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_biolab_1` | Included | — | `common/buildings/05_research_buildings.txt` | 1167 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_biolab_2` | Included | — | `common/buildings/05_research_buildings.txt` | 1255 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_biolab_3` | Included | — | `common/buildings/05_research_buildings.txt` | 1336 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_brain_node_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 682 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_brain_node_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 730 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_brain_node_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 780 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_brain_node_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 853 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_bureaucratic_1` | Included | — | `common/buildings/08_unity_buildings.txt` | 327 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_bureaucratic_2` | Included | — | `common/buildings/08_unity_buildings.txt` | 410 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_bureaucratic_3` | Included | — | `common/buildings/08_unity_buildings.txt` | 485 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_capital` | Included | — | `common/buildings/00_capital_buildings.txt` | 122 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_capital_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 94 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_carceral_test_facility` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 4490 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_center_of_guidance` | Included | — | `common/buildings/02_government_buildings.txt` | 355 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_chamber_of_silence` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 1009 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_chemical_plant` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 506 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_churning_stomach` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2460 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_churning_tunnels_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1632 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_churning_tunnels_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1675 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_churning_tunnels_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1720 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_churning_tunnels_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1791 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_citadel_of_faith` | Included | — | `common/buildings/08_unity_buildings.txt` | 3004 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_citadel_uplink` | ExcludedSpecific | biogenesis_origin_definition_file | `common/buildings/22_biogenesis_buildings.txt` | 17 | `5b8ea7adf8df561700c260477bc95efeb9883a0df2a62c97b8e415f1d71e4148` |
| Building | `building_class_3_singularity` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1468 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_class_4_singularity` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1628 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_clear_thought_clinic` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 4336 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_clone_army_clone_vat` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/01_pop_assembly_buildings.txt` | 1290 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_clone_vats` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1034 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_colony_shelter` | Included | — | `common/buildings/00_capital_buildings.txt` | 14 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_colony_shelter_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 26 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_commensal_clearing_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 925 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_commensal_clearing_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 968 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_commensal_clearing_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1025 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_commensal_clearing_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1098 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_commercial_forum` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1350 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_commercial_megaplex` | Included | — | `common/buildings/06_trade_buildings.txt` | 87 | `1167e5e2f095ab8c6d330fb5f6d623edc14b281c4a2bdaa6c5b36516fdb544a9` |
| Building | `building_commercial_zone` | Included | — | `common/buildings/06_trade_buildings.txt` | 4 | `1167e5e2f095ab8c6d330fb5f6d623edc14b281c4a2bdaa6c5b36516fdb544a9` |
| Building | `building_communal_housing` | Included | — | `common/buildings/07_amenity_buildings.txt` | 185 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_communal_housing_large` | Included | — | `common/buildings/07_amenity_buildings.txt` | 267 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_composer_sanctum` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 501 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_contained_ecosphere` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/17_paragon_buildings.txt` | 243 | `37ae0a82575c689ffe54dcce475bf81e1ed3425145effe11f58a369aec647edb` |
| Building | `building_coordinated_fulfillment_center_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 897 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_coordinated_fulfillment_center_2` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1065 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_corporate_clinics` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3918 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_corporate_embassy` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1500 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_corporate_forum` | Included | — | `common/buildings/08_unity_buildings.txt` | 2451 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_corporate_monument` | Included | — | `common/buildings/08_unity_buildings.txt` | 2256 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_corporate_site` | Included | — | `common/buildings/08_unity_buildings.txt` | 2352 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_corporate_vault` | Included | — | `common/buildings/08_unity_buildings.txt` | 3177 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_cradle_of_rebirth` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 422 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_cradle_sanctum` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 685 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_crude_huts` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 5 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_cryovault` | ExcludedSpecific | extreme_frontiers_origin_definition_file | `common/buildings/22_extreme_frontiers_buildings.txt` | 71 | `0b8aaf496361e621308ccb3a7b8df9e8a2d8d01adf2da9ecc51338afdd0fafd5` |
| Building | `building_crystal_growth` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2505 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_crystal_mines` | ExcludedSpecific | planetary_deposit_building_file | `common/buildings/10_deposit_buildings.txt` | 97 | `2d05fffe2afee8140cf1414192ff324805cfa902e9d418e1b77367e5d1398096` |
| Building | `building_crystal_plant` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 571 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_crystal_plant_2` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 310 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_cyberdome` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/20_machine_age_buildings.txt` | 2235 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_deployment_post` | Included | — | `common/buildings/00_capital_buildings.txt` | 407 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_dimensional_fabricator` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1323 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_dimensional_replicator` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1177 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_disinformation_center` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3146 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_dread_encampment` | Included | — | `common/buildings/09_army_buildings.txt` | 133 | `f3f847c877e6ce8aa64340c13f059f535105290ec7fe4cca4df71cf0360f780e` |
| Building | `building_drone_megastorage` | ExcludedSpecific | upgrade_chain_from_special_definition:building_drone_storage | `common/buildings/07_amenity_buildings.txt` | 421 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_drone_storage` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/07_amenity_buildings.txt` | 361 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_eater_sanctum` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 599 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_embassy` | Included | — | `common/buildings/02_government_buildings.txt` | 763 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_empyrean_shrine` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 424 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_energy_grid` | Included | — | `common/buildings/03_resource_buildings.txt` | 1291 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_energy_nexus` | Included | — | `common/buildings/03_resource_buildings.txt` | 1375 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_engineering_facility_1` | Included | — | `common/buildings/05_research_buildings.txt` | 1409 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_engineering_facility_2` | Included | — | `common/buildings/05_research_buildings.txt` | 1496 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_engineering_facility_3` | Included | — | `common/buildings/05_research_buildings.txt` | 1572 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_executive_retreat` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3445 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_expanded_warren` | ExcludedSpecific | upgrade_chain_from_special_definition:building_hive_warren | `common/buildings/07_amenity_buildings.txt` | 542 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_experimentation_chambers_1` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 481 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_experimentation_chambers_2` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 552 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_experimentation_chambers_3` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 620 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_factory_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 208 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_factory_2` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 313 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_factory_3` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 379 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_factory_efficiency_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1547 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_factory_upkeep_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1466 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_farming_districts_1` | Included | — | `common/buildings/03_resource_buildings.txt` | 271 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_farming_districts_2` | Included | — | `common/buildings/03_resource_buildings.txt` | 330 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_farming_districts_3` | Included | — | `common/buildings/03_resource_buildings.txt` | 393 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_farming_districts_4` | Included | — | `common/buildings/03_resource_buildings.txt` | 456 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_fe_administration_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 2724 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_administration_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 2897 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_administration_hive_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 3076 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_administration_hive_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 3238 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_administration_machine_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 3402 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_administration_machine_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 3572 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_assembly_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 4084 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_assembly_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 4307 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_clinic_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 4533 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_clinic_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 4744 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_dome` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 2257 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_entertainment_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 5923 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_entertainment_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 6075 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_fortress` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 2417 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_lab_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 6224 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_lab_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 6392 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_market_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 5311 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_market_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 5462 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_mine_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 6567 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_mine_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 6733 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_security_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 4960 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_security_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 5127 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_silo_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 5634 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_silo_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 5781 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_sky_dome` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 2093 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_stronghold` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 2566 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_temple_1` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 3745 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_temple_2` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 3909 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_fe_xeno_zoo` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 203 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_food_conglomerate` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 143 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_food_processing_center` | Included | — | `common/buildings/03_resource_buildings.txt` | 157 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_food_processing_facility` | Included | — | `common/buildings/03_resource_buildings.txt` | 69 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_forever_cruise_crew_industry` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 510 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_forever_cruise_crew_resources` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 449 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_forever_cruise_crew_service` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 576 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_forever_cruise_passenger_consumer_goods` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 392 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_forever_cruise_passenger_research` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 333 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_forever_cruise_passenger_unity` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 268 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_forge_of_the_fellowship` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 874 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_fortress` | Included | — | `common/buildings/09_army_buildings.txt` | 252 | `f3f847c877e6ce8aa64340c13f059f535105290ec7fe4cca4df71cf0360f780e` |
| Building | `building_foundry_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 4 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_foundry_2` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 94 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_foundry_3` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 155 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_foundry_efficiency_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1372 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_foundry_upkeep_1` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1292 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_gaiaseeders_1` | Included | — | `common/buildings/02_government_buildings.txt` | 917 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_gaiaseeders_2` | Included | — | `common/buildings/02_government_buildings.txt` | 996 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_gaiaseeders_3` | Included | — | `common/buildings/02_government_buildings.txt` | 1063 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_gaiaseeders_4` | Included | — | `common/buildings/02_government_buildings.txt` | 1129 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_gaiaseeders_pc_gaia` | Included | — | `common/buildings/02_government_buildings.txt` | 1208 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_galactic_memorial_1` | Included | — | `common/buildings/08_unity_buildings.txt` | 1544 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_galactic_memorial_2` | Included | — | `common/buildings/08_unity_buildings.txt` | 1683 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_galactic_memorial_3` | Included | — | `common/buildings/08_unity_buildings.txt` | 1831 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_galactic_stock_exchange` | Included | — | `common/buildings/06_trade_buildings.txt` | 168 | `1167e5e2f095ab8c6d330fb5f6d623edc14b281c4a2bdaa6c5b36516fdb544a9` |
| Building | `building_gas_extractors` | ExcludedSpecific | planetary_deposit_building_file | `common/buildings/10_deposit_buildings.txt` | 51 | `2d05fffe2afee8140cf1414192ff324805cfa902e9d418e1b77367e5d1398096` |
| Building | `building_generator_districts_1` | Included | — | `common/buildings/03_resource_buildings.txt` | 1487 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_generator_districts_2` | Included | — | `common/buildings/03_resource_buildings.txt` | 1546 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_generator_districts_3` | Included | — | `common/buildings/03_resource_buildings.txt` | 1609 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_generator_districts_4` | Included | — | `common/buildings/03_resource_buildings.txt` | 1672 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_generator_generic` | Included | — | `common/buildings/03_resource_buildings.txt` | 1238 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_genomic_facility` | ExcludedSpecific | biogenesis_origin_definition_file | `common/buildings/22_biogenesis_buildings.txt` | 158 | `5b8ea7adf8df561700c260477bc95efeb9883a0df2a62c97b8e415f1d71e4148` |
| Building | `building_grand_battlements_of_steel` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 602 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_grand_cathedral_of_toil` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 794 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_grand_concert_hall_of_the_mind` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 393 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_grand_embassy` | Included | — | `common/buildings/02_government_buildings.txt` | 843 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_grand_forge_of_the_fellowship` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 990 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_great_pyramid` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 87 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_hab_capital` | ExcludedSpecific | source_id_special_family:building_hab | `common/buildings/00_capital_buildings.txt` | 1010 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_hab_fe_capital` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 256 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_hab_major_capital` | ExcludedSpecific | source_id_special_family:building_hab | `common/buildings/00_capital_buildings.txt` | 1146 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_hab_system_capital` | ExcludedSpecific | source_id_special_family:building_hab | `common/buildings/00_capital_buildings.txt` | 1292 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_hall_judgment` | Included | — | `common/buildings/02_government_buildings.txt` | 217 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_heritage_site` | Included | — | `common/buildings/08_unity_buildings.txt` | 112 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_hive_capital` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/00_capital_buildings.txt` | 833 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_hive_cluster` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/08_unity_buildings.txt` | 1152 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_hive_confluence` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/08_unity_buildings.txt` | 1234 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_hive_crude_huts` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 620 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_major_capital` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/00_capital_buildings.txt` | 922 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_hive_node` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/08_unity_buildings.txt` | 1077 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_hive_primitive_capital` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 966 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_primitive_dwellings` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 664 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_primitive_factory` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 772 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_primitive_farm` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 916 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_primitive_mine` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 817 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_primitive_node` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1100 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_primitive_power_plant` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 871 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_stone_palace` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 709 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_transcendental_retreat` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/20_machine_age_buildings.txt` | 173 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_hive_urban_dwellings` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1055 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_hive_warren` | ExcludedSpecific | source_id_special_family:building_hive | `common/buildings/07_amenity_buildings.txt` | 481 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_holo_theatres` | Included | — | `common/buildings/07_amenity_buildings.txt` | 609 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_holotemple` | Included | — | `common/buildings/08_unity_buildings.txt` | 671 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_hydroponics_farm` | Included | — | `common/buildings/03_resource_buildings.txt` | 5 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_hyper_entertainment_forum` | Included | — | `common/buildings/07_amenity_buildings.txt` | 679 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_hypercomms_forum` | Included | — | `common/buildings/08_unity_buildings.txt` | 219 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_identity_complex` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/20_machine_age_buildings.txt` | 2319 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_identity_repository` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 1257 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_illicit_research_labs` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 2883 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_imperial_capital` | Included | — | `common/buildings/00_capital_buildings.txt` | 1733 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_imperial_capital_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 346 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_imperial_concession_port` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3716 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_imperial_hive_capital` | ExcludedSpecific | source_id_special_family:gestalt_or_primitive | `common/buildings/00_capital_buildings.txt` | 1939 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_imperial_machine_capital` | ExcludedSpecific | source_id_special_family:gestalt_or_primitive | `common/buildings/00_capital_buildings.txt` | 1823 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_industrial_subsidiary` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 796 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_institute` | Included | — | `common/buildings/05_research_buildings.txt` | 216 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_instrument_sanctum` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 684 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_junkheap` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 5 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_knightly_theme_park` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3795 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_lathe_capital` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1421 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_cogitator` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1893 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_life_support` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1851 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_major_capital` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1468 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_overclocker` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1718 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_preserver` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1772 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_reactor` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1813 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_resonator` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1632 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_stabilisator` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1681 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_super_capital` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1544 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_lathe_validator` | ExcludedSpecific | source_id_special_family:building_lathe | `common/buildings/20_machine_age_buildings.txt` | 1948 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_league_offices` | Included | — | `common/buildings/08_unity_buildings.txt` | 3890 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_lifecrypt_1` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 6 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_2` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 49 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_3` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 92 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_corporate_1` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 348 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_corporate_2` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 392 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_corporate_3` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 436 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_hive_mind_1` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 240 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_hive_mind_2` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 277 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_hive_mind_3` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 314 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_machine_1` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 132 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_machine_2` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 169 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_lifecrypt_machine_3` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 206 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_living_metal_clinic` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 4653 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_low_tech_admin_hub` | ExcludedSpecific | first_contact_origin_definition_file | `common/buildings/16_first_contact_buildings.txt` | 63 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| Building | `building_low_tech_capital` | ExcludedSpecific | first_contact_origin_definition_file | `common/buildings/16_first_contact_buildings.txt` | 207 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| Building | `building_low_tech_farm` | ExcludedSpecific | first_contact_origin_definition_file | `common/buildings/16_first_contact_buildings.txt` | 160 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| Building | `building_low_tech_power_plant` | ExcludedSpecific | first_contact_origin_definition_file | `common/buildings/16_first_contact_buildings.txt` | 113 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| Building | `building_low_tech_research_lab` | ExcludedSpecific | first_contact_origin_definition_file | `common/buildings/16_first_contact_buildings.txt` | 258 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| Building | `building_low_tech_scrap_refinery` | ExcludedSpecific | first_contact_origin_definition_file | `common/buildings/16_first_contact_buildings.txt` | 5 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| Building | `building_luxury_residence` | Included | — | `common/buildings/07_amenity_buildings.txt` | 4 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_machine_assembly_complex` | ExcludedSpecific | source_id_special_family:building_machine | `common/buildings/01_pop_assembly_buildings.txt` | 481 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_machine_assembly_plant` | ExcludedSpecific | source_id_special_family:building_machine | `common/buildings/01_pop_assembly_buildings.txt` | 373 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_machine_capital` | ExcludedSpecific | source_id_special_family:building_machine | `common/buildings/00_capital_buildings.txt` | 488 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_machine_major_capital` | ExcludedSpecific | source_id_special_family:building_machine | `common/buildings/00_capital_buildings.txt` | 603 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_machine_primitive_node` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1135 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_machine_system_capital` | ExcludedSpecific | source_id_special_family:building_machine | `common/buildings/00_capital_buildings.txt` | 707 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_maintenance_depot` | ExcludedSpecific | root_eligibility:is_gestalt=yes | `common/buildings/06_trade_buildings.txt` | 252 | `1167e5e2f095ab8c6d330fb5f6d623edc14b281c4a2bdaa6c5b36516fdb544a9` |
| Building | `building_major_capital` | Included | — | `common/buildings/00_capital_buildings.txt` | 217 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_major_capital_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 183 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_massive_growth_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1164 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_massive_growth_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1206 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_massive_growth_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1250 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_massive_growth_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1326 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_master_archive` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 340 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_materiality_engine` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 921 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_medical_1` | Included | — | `common/buildings/07_amenity_buildings.txt` | 732 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_medical_2` | Included | — | `common/buildings/07_amenity_buildings.txt` | 826 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_medical_3` | Included | — | `common/buildings/07_amenity_buildings.txt` | 1047 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_micro_forge` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1783 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_military_academy` | Included | — | `common/buildings/09_army_buildings.txt` | 4 | `f3f847c877e6ce8aa64340c13f059f535105290ec7fe4cca4df71cf0360f780e` |
| Building | `building_military_contractors` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 604 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_mine_generic` | Included | — | `common/buildings/03_resource_buildings.txt` | 634 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_mineral_purification_hub` | Included | — | `common/buildings/03_resource_buildings.txt` | 825 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_mineral_purification_plant` | Included | — | `common/buildings/03_resource_buildings.txt` | 691 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_mining_districts_1` | Included | — | `common/buildings/03_resource_buildings.txt` | 994 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_mining_districts_2` | Included | — | `common/buildings/03_resource_buildings.txt` | 1053 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_mining_districts_3` | Included | — | `common/buildings/03_resource_buildings.txt` | 1116 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_mining_districts_4` | Included | — | `common/buildings/03_resource_buildings.txt` | 1179 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_ministry_production` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 720 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_mote_aggravator` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2415 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_mote_harvesters` | ExcludedSpecific | planetary_deposit_building_file | `common/buildings/10_deposit_buildings.txt` | 5 | `2d05fffe2afee8140cf1414192ff324805cfa902e9d418e1b77367e5d1398096` |
| Building | `building_nanite_transmuter` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 641 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_nano_forge` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1933 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_nanolab_1` | Included | — | `common/buildings/20_machine_age_buildings.txt` | 2376 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_nanolab_2` | Included | — | `common/buildings/20_machine_age_buildings.txt` | 2451 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_nanotech_cauldron` | Included | — | `common/buildings/20_machine_age_buildings.txt` | 2528 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_natural_furnace_0` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1861 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_natural_furnace_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1905 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_natural_furnace_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 1958 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_natural_furnace_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2025 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_necrophage_elevation_chamber` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/01_pop_assembly_buildings.txt` | 759 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_necrophage_house_of_apotheosis` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/01_pop_assembly_buildings.txt` | 902 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_network_junction` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 925 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_noble_estates` | Included | — | `common/buildings/02_government_buildings.txt` | 4 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_nourishment_center` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 1017 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_nourishment_complex` | ExcludedSpecific | fallen_empire_definition_file | `common/buildings/13_fallen_empire_buildings.txt` | 856 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| Building | `building_nuumismatic_shrine` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 256 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_offspring_nest` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/01_pop_assembly_buildings.txt` | 675 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_offworld_expedition_hub` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 1642 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_order_castle` | Included | — | `common/buildings/02_government_buildings.txt` | 621 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_order_keep` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/02_government_buildings.txt` | 484 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_organic_paradise` | Included | — | `common/buildings/08_unity_buildings.txt` | 3666 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_organic_sanctuary` | Included | — | `common/buildings/08_unity_buildings.txt` | 3515 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_overseer_homes` | Included | — | `common/buildings/07_amenity_buildings.txt` | 1188 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_ozone_thickener` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2657 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_paradise_dome` | Included | — | `common/buildings/07_amenity_buildings.txt` | 91 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_paragon_memory_vaults` | ExcludedSpecific | root_eligibility:is_memory_vault_empire=yes | `common/buildings/17_paragon_buildings.txt` | 9 | `37ae0a82575c689ffe54dcce475bf81e1ed3425145effe11f58a369aec647edb` |
| Building | `building_passenger_dorms` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 5 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_passenger_paradise` | ExcludedSpecific | nomad_definition_file | `common/buildings/24_nomads_buildings.txt` | 137 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| Building | `building_physics_lab_1` | Included | — | `common/buildings/05_research_buildings.txt` | 930 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_physics_lab_2` | Included | — | `common/buildings/05_research_buildings.txt` | 1017 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_physics_lab_3` | Included | — | `common/buildings/05_research_buildings.txt` | 1094 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_pinniped_sanctuary` | ExcludedSpecific | extreme_frontiers_origin_definition_file | `common/buildings/22_extreme_frontiers_buildings.txt` | 5 | `0b8aaf496361e621308ccb3a7b8df9e8a2d8d01adf2da9ecc51338afdd0fafd5` |
| Building | `building_pirate_haven` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 2426 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_planetary_carapace` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2554 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_planetary_carapace_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2604 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_planetary_shield_generator` | Included | — | `common/buildings/09_army_buildings.txt` | 304 | `f3f847c877e6ce8aa64340c13f059f535105290ec7fe4cca4df71cf0360f780e` |
| Building | `building_posthumous_employment_center` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 1493 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_pre_ftl_radio_telescope` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 562 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_precinct_house` | Included | — | `common/buildings/02_government_buildings.txt` | 136 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_primitive_capital` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 427 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_clinic` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1170 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_dwellings` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 48 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_factory` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 154 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_farm` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 379 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_mine` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 282 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_offices` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 203 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_power_plant` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 335 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_primitive_research` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 237 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_private_mining_consortium` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 16 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_private_research_initiative` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1059 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_private_security` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1673 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_private_shipyards` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 466 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_production_center` | ExcludedSpecific | root_eligibility:is_gestalt=yes | `common/buildings/04_manufacturing_buildings.txt` | 812 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_psi_corps` | Included | — | `common/buildings/02_government_buildings.txt` | 669 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_psionic_offices` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 4537 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_psionic_suppressor` | ExcludedSpecific | shroud_scenario_definition_file | `common/buildings/23_shroud_buildings.txt` | 753 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| Building | `building_public_relations_office` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 922 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_ranger_lodge` | Included | — | `common/buildings/05_research_buildings.txt` | 1645 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_refinery` | Included | — | `common/buildings/04_manufacturing_buildings.txt` | 442 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| Building | `building_research_efficiency_1` | Included | — | `common/buildings/05_research_buildings.txt` | 1893 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_research_lab_1` | Included | — | `common/buildings/05_research_buildings.txt` | 4 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_research_lab_2` | Included | — | `common/buildings/05_research_buildings.txt` | 94 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_research_lab_3` | Included | — | `common/buildings/05_research_buildings.txt` | 157 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_research_upkeep_1` | Included | — | `common/buildings/05_research_buildings.txt` | 1796 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_resort_capital` | ExcludedSpecific | source_id_special_family:building_resort | `common/buildings/00_capital_buildings.txt` | 1442 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_resort_major_capital` | ExcludedSpecific | source_id_special_family:building_resort | `common/buildings/00_capital_buildings.txt` | 1517 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_resource_silo` | Included | — | `common/buildings/03_resource_buildings.txt` | 1731 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| Building | `building_robot_assembly_complex` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 194 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_robot_assembly_plant` | Included | — | `common/buildings/01_pop_assembly_buildings.txt` | 5 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_sacred_nexus` | Included | — | `common/buildings/08_unity_buildings.txt` | 757 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sacrificial_temple_1` | Included | — | `common/buildings/08_unity_buildings.txt` | 1316 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sacrificial_temple_2` | Included | — | `common/buildings/08_unity_buildings.txt` | 1392 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sacrificial_temple_3` | Included | — | `common/buildings/08_unity_buildings.txt` | 1466 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sanctuary_of_toil` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 687 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_sensorium_1` | ExcludedSpecific | root_eligibility:is_hive_empire=yes | `common/buildings/08_unity_buildings.txt` | 2549 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sensorium_2` | ExcludedSpecific | root_eligibility:is_hive_empire=yes | `common/buildings/08_unity_buildings.txt` | 2640 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sensorium_3` | ExcludedSpecific | root_eligibility:is_hive_empire=yes | `common/buildings/08_unity_buildings.txt` | 2735 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_sentinel_posts` | ExcludedSpecific | root_eligibility:is_gestalt=yes | `common/buildings/02_government_buildings.txt` | 421 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_shroud_observatory_1` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/05_research_buildings.txt` | 455 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_shroud_observatory_2` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/05_research_buildings.txt` | 515 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_shroud_observatory_3` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/05_research_buildings.txt` | 586 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_simulation_1` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 1975 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_simulation_2` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 2068 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_simulation_3` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 2165 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_slave_capital` | ExcludedSpecific | source_id_thrall_world_capital | `common/buildings/00_capital_buildings.txt` | 1602 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_slave_major_capital` | ExcludedSpecific | source_id_thrall_world_capital | `common/buildings/00_capital_buildings.txt` | 1662 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_slave_processing` | Included | — | `common/buildings/02_government_buildings.txt` | 63 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_smuggling_rings` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3015 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_solarpunk_gaiaseeder` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1245 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_solarpunk_organic_paradise` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1422 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_solarpunk_ranger_lodge` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1301 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_solarpunk_sapling` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 1361 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_spawning_pool` | ExcludedSpecific | root_eligibility:is_hive_empire=yes | `common/buildings/01_pop_assembly_buildings.txt` | 577 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| Building | `building_state_academy` | Included | — | `common/buildings/02_government_buildings.txt` | 290 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_stone_palace` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 92 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_storm_attraction_center` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 5 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_storm_grand_theater` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 926 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_storm_holo_theater` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 832 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_storm_repellent_center` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 216 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_storm_resistant_production` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 353 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_storm_summoning_theater` | Included | — | `common/buildings/19_cosmic_storm_buildings.txt` | 758 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| Building | `building_stronghold` | Included | — | `common/buildings/09_army_buildings.txt` | 185 | `f3f847c877e6ce8aa64340c13f059f535105290ec7fe4cca4df71cf0360f780e` |
| Building | `building_subterranean_cache` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2839 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_subversive_shrine` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3299 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_supercomputer` | ExcludedSpecific | root_eligibility:is_gestalt=yes | `common/buildings/05_research_buildings.txt` | 334 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_syndicate_outreach_office` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 2760 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_system_capital` | Included | — | `common/buildings/00_capital_buildings.txt` | 309 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| Building | `building_system_capital_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 266 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_system_conflux` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 1000 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_temple` | Included | — | `common/buildings/08_unity_buildings.txt` | 576 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_temple_of_prosperity` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3373 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_tendril_cradle_1` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 473 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_tendril_cradle_2` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 520 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_tendril_cradle_3` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 571 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_tendril_cradle_4` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 626 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_the_beholder` | ExcludedSpecific | source_event_target:paragon_story_building | `common/buildings/17_paragon_buildings.txt` | 165 | `37ae0a82575c689ffe54dcce475bf81e1ed3425145effe11f58a369aec647edb` |
| Building | `building_the_sanctum_of_augmentation` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 1082 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_the_united_sanctum_of_augmentation` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/20_machine_age_buildings.txt` | 1182 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_toxic_bath` | ExcludedSpecific | root_eligibility:is_toxic_baths_empire=yes | `common/buildings/07_amenity_buildings.txt` | 1230 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_toxic_bath_hive` | ExcludedSpecific | source_id_special_family:gestalt | `common/buildings/07_amenity_buildings.txt` | 1314 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_toxic_bath_machine` | ExcludedSpecific | source_id_special_family:gestalt | `common/buildings/07_amenity_buildings.txt` | 1379 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| Building | `building_transcendental_retreat` | Included | — | `common/buildings/20_machine_age_buildings.txt` | 89 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| Building | `building_trophy_vault` | Included | — | `common/buildings/08_unity_buildings.txt` | 3784 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_underground_chemists` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 2125 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_underground_clubs` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 2623 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_uplink_node` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/buildings/08_unity_buildings.txt` | 848 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| Building | `building_urban_dwellings` | ExcludedSpecific | primitive_civilization_building_file | `common/buildings/11_primitive_buildings.txt` | 517 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| Building | `building_virtual_entertainment_studios` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 310 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_volcanic_forge_1` | Included | — | `common/buildings/02_government_buildings.txt` | 1317 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_volcanic_forge_2` | Included | — | `common/buildings/02_government_buildings.txt` | 1394 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| Building | `building_vultaum_reality_computer` | Included | — | `common/buildings/05_research_buildings.txt` | 834 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| Building | `building_waste_reprocessing_center` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 196 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_whisperers_sanctum` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 786 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `building_wildcat_miners` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 1857 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_wilderness_storm_relief` | ExcludedSpecific | wilderness_definition_file | `common/buildings/21_wilderness_buildings.txt` | 2737 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| Building | `building_wrecking_yards` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 2285 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_xeno_tourism_agency` | ExcludedSpecific | branch_office_definition_file | `common/buildings/14_branch_office_buildings.txt` | 3598 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| Building | `building_xeno_zoo` | ExcludedSpecific | planetary_deposit_building_file | `common/buildings/10_deposit_buildings.txt` | 195 | `2d05fffe2afee8140cf1414192ff324805cfa902e9d418e1b77367e5d1398096` |
| Building | `building_zroni_equilibrator` | ExcludedSpecific | event_definition_file | `common/buildings/12_event_buildings.txt` | 381 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| Building | `galactic_history_holomuseum` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/21_grand_archive_buildings.txt` | 259 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `holding_aid_agency` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 246 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_communal_housing_outreach` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1158 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_distributed_processing` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 621 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_dread_outpost` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1091 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_emporium` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 197 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_energy_requisitorium` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 341 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_experimental_crater` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1636 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_franchise_headquarters` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1733 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_garrison` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_idyllic_bloom` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1235 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_knight_commandery` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 2088 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_material_requisitorium` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 398 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_noble_vacation_homes` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1025 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_offspring_nest` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 675 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_offworld_foundry` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 729 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_orbital_assembly_complex` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 66 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_organic_sanctuary` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1686 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_overlord_vigil_command` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 836 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_parasitic_algorithms` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 947 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_park_ranger_lodge` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1478 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_produce_requisitorium` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 459 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_propaganda_office` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 103 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_recruitment_office` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1416 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_reemployment_center` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1352 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_sacrificial_shrine` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1868 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_satellite_campus` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 153 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_splinter_hive` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 532 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_transcendental_retreat` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 2161 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_tree_of_life_sapling` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 1568 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `holding_wilderness_glade` | ExcludedSpecific | overlord_holding_definition_file | `common/buildings/15_overlord_holdings.txt` | 2250 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| Building | `hunting_grounds` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 588 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `hunting_grounds_2` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 662 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `hunting_grounds_3` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 744 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `primal_arena` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 995 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `primal_arena_2` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 1048 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `primal_arena_3` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 1110 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `symbiosis_nexus` | ExcludedSpecific | root_eligibility:special_symbiosis_job | `common/buildings/21_grand_archive_buildings.txt` | 1168 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `symbiosis_nexus_2` | ExcludedSpecific | upgrade_chain_from_special_definition:symbiosis_nexus | `common/buildings/21_grand_archive_buildings.txt` | 1222 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `symbiosis_nexus_3` | ExcludedSpecific | upgrade_chain_from_special_definition:symbiosis_nexus_2 | `common/buildings/21_grand_archive_buildings.txt` | 1285 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `wildlife_ranch` | ExcludedSpecific | root_eligibility:specific_origin | `common/buildings/21_grand_archive_buildings.txt` | 386 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `wildlife_ranch_2` | ExcludedSpecific | upgrade_chain_from_special_definition:wildlife_ranch | `common/buildings/21_grand_archive_buildings.txt` | 450 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `wildlife_ranch_3` | ExcludedSpecific | upgrade_chain_from_special_definition:wildlife_ranch_2 | `common/buildings/21_grand_archive_buildings.txt` | 521 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `wildlife_sanctuary` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 822 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `wildlife_sanctuary_2` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 875 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `wildlife_sanctuary_3` | Included | — | `common/buildings/21_grand_archive_buildings.txt` | 937 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Building | `xeno_geology_holomuseum` | ExcludedSpecific | root_eligibility:scenario_country_flag | `common/buildings/21_grand_archive_buildings.txt` | 5 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| Zone | `zone_agrarian_anglers` | Included | — | `common/zones/00_zones.txt` | 1426 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_agrarian_urban` | Included | — | `common/zones/00_zones.txt` | 1350 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_anglers` | Included | — | `common/zones/00_zones.txt` | 1111 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_ark_ammunition_lines` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 599 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_engine_bypass` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 331 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_farm_society` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 444 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_flight_decks` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 707 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_food` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 428 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_forever_cruise_city_crew` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 959 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_forever_cruise_city_mixed` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 971 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_forever_cruise_city_passenger` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 947 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_forever_cruise_crew` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 898 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_forever_cruise_engineering` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 910 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_forever_cruise_passenger` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 886 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_foundry` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 250 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_harvest` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 479 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_industrial` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 86 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_knights` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 1019 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_laser_capacitors` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 653 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_military_eco` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 761 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_military_engineering` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 848 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_missile_silos` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 545 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_reactor_physics` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 391 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_research_unity` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 2 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_research_unity_gestalt` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 169 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_sacred_path_pilgrims` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 988 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_shield_overloader` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 360 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_shipwright` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 782 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_ark_waystation_trade` | ExcludedSpecific | arkship_definition_file | `common/zones/05_arkship_zones.txt` | 493 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| Zone | `zone_betharian` | Included | — | `common/zones/00_zones.txt` | 965 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_betharian_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1259 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_betharian_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 900 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_broken_shackles_memorial` | ExcludedSpecific | unique_scenario_zone_file | `common/zones/02_special_zones.txt` | 68 | `54010b2dd802f96a0634106e5cbe682b4eb7d7143ed0f2bcb3930e69a2a63fd4` |
| Zone | `zone_central_spire` | ExcludedSpecific | unique_scenario_zone_file | `common/zones/02_special_zones.txt` | 140 | `54010b2dd802f96a0634106e5cbe682b4eb7d7143ed0f2bcb3930e69a2a63fd4` |
| Zone | `zone_cosmogenesis_default` | ExcludedSpecific | definition_id:special_gestalt_or_scenario | `common/zones/00_zones.txt` | 1496 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_default` | Included | — | `common/zones/00_zones.txt` | 5 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_energy` | Included | — | `common/zones/00_zones.txt` | 1063 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_energy_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1307 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_energy_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 948 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_energy_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 613 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_energy_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 422 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_exotic_gases` | Included | — | `common/zones/00_zones.txt` | 1181 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_exotic_gases_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1217 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_exotic_gases_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 985 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_factory` | Included | — | `common/zones/00_zones.txt` | 170 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_factory_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 62 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_factory_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1043 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_factory_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 670 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_factory_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 388 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_food` | Included | — | `common/zones/00_zones.txt` | 1098 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_food_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1199 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_food_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 595 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_food_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 478 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_fortress` | Included | — | `common/zones/00_zones.txt` | 417 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_fortress_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 297 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_fortress_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1163 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_fortress_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 841 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_fortress_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 559 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_fortress_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 317 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_foundry` | Included | — | `common/zones/00_zones.txt` | 151 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_foundry_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 35 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_foundry_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1025 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_foundry_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 652 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_foundry_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 370 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_foundry_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 61 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_habitat_exotic_gases` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 243 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_habitat_hydroponics` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 77 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_habitat_knights` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 2 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_habitat_rare_crystals` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 215 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_habitat_research` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 129 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_habitat_research_unity` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 165 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_habitat_volatile_motes` | ExcludedSpecific | habitat_definition_file | `common/zones/01_habitat_zones.txt` | 229 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| Zone | `zone_industrial` | Included | — | `common/zones/00_zones.txt` | 132 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_industrial_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 9 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_industrial_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1006 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_industrial_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 633 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_industrial_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 351 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_machine_replication` | ExcludedSpecific | root_eligibility:is_machine_empire=yes | `common/zones/00_zones.txt` | 1218 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_minerals` | Included | — | `common/zones/00_zones.txt` | 947 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_minerals_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1236 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_minerals_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 877 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_minerals_physics` | ExcludedSpecific | root_eligibility:planetary_deposit_requirement | `common/zones/00_zones.txt` | 1001 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_minerals_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 369 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_payback_enlightenment` | ExcludedSpecific | unique_scenario_zone_file | `common/zones/02_special_zones.txt` | 2 | `54010b2dd802f96a0634106e5cbe682b4eb7d7143ed0f2bcb3930e69a2a63fd4` |
| Zone | `zone_rare_crystals` | Included | — | `common/zones/00_zones.txt` | 981 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_rare_crystals_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1283 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_rare_crystals_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 924 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research` | Included | — | `common/zones/00_zones.txt` | 258 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_research_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 88 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_engineering` | Included | — | `common/zones/00_zones.txt` | 326 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_research_engineering_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 174 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_engineering_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1123 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_engineering_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 750 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_engineering_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 468 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1061 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 688 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_physics` | Included | — | `common/zones/00_zones.txt` | 276 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_research_physics_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 114 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_physics_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1079 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_physics_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 706 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_physics_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 424 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 406 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_society` | Included | — | `common/zones/00_zones.txt` | 301 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_research_society_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 144 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_society_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1101 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_society_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 728 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_society_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 446 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_research_unity` | Included | — | `common/zones/00_zones.txt` | 186 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_research_unity_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 208 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_research_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 106 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_resort` | ExcludedSpecific | root_eligibility:special_resort_colony_modifier | `common/zones/00_zones.txt` | 453 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_entertainment` | ExcludedSpecific | root_eligibility:special_resort_colony_modifier | `common/zones/00_zones.txt` | 504 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_grand_museum` | Included | — | `common/zones/00_zones.txt` | 626 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_hunting_ground` | Included | — | `common/zones/00_zones.txt` | 689 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_proving_grounds` | Included | — | `common/zones/00_zones.txt` | 881 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_restoration_enclave` | Included | — | `common/zones/00_zones.txt` | 818 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_spiritual_retreat` | Included | — | `common/zones/00_zones.txt` | 754 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_resort_zoo` | ExcludedSpecific | root_eligibility:special_resort_colony_modifier | `common/zones/00_zones.txt` | 564 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_spawning` | ExcludedSpecific | definition_id:special_gestalt_or_scenario | `common/zones/00_zones.txt` | 1202 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_spawning_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1344 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_subterranean_urban` | ExcludedSpecific | root_eligibility:is_subterranean_empire=yes | `common/zones/00_zones.txt` | 1290 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_trade` | Included | — | `common/zones/00_zones.txt` | 433 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_trade_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 323 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_trade_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1181 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_trade_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 859 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_trade_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 577 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_trade_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 266 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_unity` | Included | — | `common/zones/00_zones.txt` | 351 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_unity_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 204 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_bio_trophy` | ExcludedSpecific | source_id_special_family:planet_class_or_gestalt | `common/zones/00_zones.txt` | 395 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_unity_bio_trophy_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 266 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_bio_trophy_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 818 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_bio_trophy_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 536 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1145 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 772 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 490 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_spiritualist` | Included | — | `common/zones/00_zones.txt` | 373 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_unity_spiritualist_arcology` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 235 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_spiritualist_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 795 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_spiritualist_ring_world` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 513 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_unity_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 157 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_urban` | Included | — | `common/zones/00_zones.txt` | 57 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_urban_wilderness` | ExcludedSpecific | wilderness_definition_file | `common/zones/03_wilderness_zones.txt` | 5 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| Zone | `zone_volatile_motes` | Included | — | `common/zones/00_zones.txt` | 1078 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| Zone | `zone_volatile_motes_hive` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 1325 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| Zone | `zone_volatile_motes_nexus` | ExcludedSpecific | special_planet_class_or_gestalt_zone_file | `common/zones/04_secondary_zones.txt` | 966 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |

## Retained runtime expressions

The catalog retains 571 source expressions; active ordinary production/upkeep without a resolved decimal: 0. Unknown expressions remain typed and are not reduced to zero.
- Ordinary `biologist` capacity: `value:natural_blocker_count` — `common/buildings/05_research_buildings.txt:1730`, SHA-256 `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5`.

## Transitive workforce job closure

Capacity targets: 59; Covered 29; Gated 30; Missing 0. Imported source job recipes: 31. Closure flow resources: 15; Covered 12; Gated 3; Missing 0.
Covered means the source recipe was imported; its ordinary and runtime-availability conditions remain typed in the job. Gated means a special source file, inline-only category, or inactive ordinary availability; a gated source is not activated as an ordinary job. Missing means there is no definition for the capacity target, and it is not replaced with an empty/zero recipe.

| Job target | Status | Source/gate | Line | SHA-256 |
|---|---|---|---:|---|
| `agri_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 2216 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `ark_harvester` | Gated | special_job_source_file:17_nomads_jobs.txt `common/pop_jobs/17_nomads_jobs.txt` | 381 | `74f27999909748943c88e1bd40edc9e6f1185568a4ea5ad5626d5df63de3d0c8` |
| `ark_harvester_gestalt` | Gated | special_job_source_file:17_nomads_jobs.txt `common/pop_jobs/17_nomads_jobs.txt` | 558 | `74f27999909748943c88e1bd40edc9e6f1185568a4ea5ad5626d5df63de3d0c8` |
| `artisan` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 2611 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `artisan_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 1333 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `augmentor` | Gated | special_job_source_file:13_machine_age_jobs.txt `common/pop_jobs/13_machine_age_jobs.txt` | 236 | `5b4963303f53e7fe5d654c565e4aadd9aec3311787e0a3ff468afc8b6b01c1ea` |
| `augmentor_drone` | Gated | special_job_source_file:13_machine_age_jobs.txt `common/pop_jobs/13_machine_age_jobs.txt` | 334 | `5b4963303f53e7fe5d654c565e4aadd9aec3311787e0a3ff468afc8b6b01c1ea` |
| `battle_thrall` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/02_specialist_jobs.txt` | 3436 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `bio_trophy` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/00_other_jobs.txt` | 521 | `62a63b3e17c111ad475b92193eaa80ec727375e95ba3b04b30883d08215e0a3f` |
| `biologist` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 244 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `bureaucrat` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 588 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `calculator_biologist` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 922 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `calculator_engineer` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 1171 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `calculator_physicist` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 642 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `clerk` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/03_worker_jobs.txt` | 19 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `colonist` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/03_worker_jobs.txt` | 5 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `coordinator` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 449 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `drone_wrangler` | Gated | special_job_source_file:14_grand_archive_jobs.txt `common/pop_jobs/14_grand_archive_jobs.txt` | 234 | `8b5bcf1f6c470b765e178fcdad131c28cb7c6dd504609802761357159092a859` |
| `dystopian_enforcer` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/01_ruler_jobs.txt` | 519 | `58a4b8595e692216e2c4faba8fbf29975880fe0bfb18cb4bd5e257d35877331f` |
| `dystopian_telepath` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/01_ruler_jobs.txt` | 620 | `58a4b8595e692216e2c4faba8fbf29975880fe0bfb18cb4bd5e257d35877331f` |
| `enforcer` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 1285 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `engineer` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 429 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `entertainer` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 1441 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `fabricator` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 1417 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `farmer` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/03_worker_jobs.txt` | 452 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `fe_acolyte_farm` | Gated | special_job_source_file:07_fallen_empire_jobs.txt `common/pop_jobs/07_fallen_empire_jobs.txt` | 256 | `6c86bcdcc5f25b87027d72a561f9623ecd4ca1d4ba02b391c6004756322a5064` |
| `fe_acolyte_generator` | Gated | special_job_source_file:07_fallen_empire_jobs.txt `common/pop_jobs/07_fallen_empire_jobs.txt` | 384 | `6c86bcdcc5f25b87027d72a561f9623ecd4ca1d4ba02b391c6004756322a5064` |
| `fe_acolyte_mine` | Gated | special_job_source_file:07_fallen_empire_jobs.txt `common/pop_jobs/07_fallen_empire_jobs.txt` | 320 | `6c86bcdcc5f25b87027d72a561f9623ecd4ca1d4ba02b391c6004756322a5064` |
| `fe_archivist` | Gated | special_job_source_file:07_fallen_empire_jobs.txt `common/pop_jobs/07_fallen_empire_jobs.txt` | 205 | `6c86bcdcc5f25b87027d72a561f9623ecd4ca1d4ba02b391c6004756322a5064` |
| `foundry` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 2514 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `healthcare` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 2056 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `knight` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 2992 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `knight_commander` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/01_ruler_jobs.txt` | 445 | `58a4b8595e692216e2c4faba8fbf29975880fe0bfb18cb4bd5e257d35877331f` |
| `logistics_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 1934 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `miner` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/03_worker_jobs.txt` | 227 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `mining_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 2054 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `mortal_initiate` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/03_worker_jobs.txt` | 631 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `offworld_prospector` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 3280 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `offworld_prospector_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 2892 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `patrol_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 1779 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `physician_drone` | Gated | special_job_source_file:16_shroud_jobs.txt `common/pop_jobs/16_shroud_jobs.txt` | 42 | `868480705cb8800c79ebce4f976fe186bb68d89783a4a5b455581478cd5928ef` |
| `physicist` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 4 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `politician` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/01_ruler_jobs.txt` | 5 | `58a4b8595e692216e2c4faba8fbf29975880fe0bfb18cb4bd5e257d35877331f` |
| `polytechnic_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 2832 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `polytechnic_mentor` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 3203 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `production_overseer` | Gated | special_job_source_file:16_shroud_jobs.txt `common/pop_jobs/16_shroud_jobs.txt` | 577 | `868480705cb8800c79ebce4f976fe186bb68d89783a4a5b455581478cd5928ef` |
| `reassigner` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 2817 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `replicator` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 221 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `roboticist` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 1889 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `slave_overseer` | Gated | source_job_availability:InactiveInOrdinaryContext `common/pop_jobs/00_other_jobs.txt` | 172 | `62a63b3e17c111ad475b92193eaa80ec727375e95ba3b04b30883d08215e0a3f` |
| `soldier` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 3317 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `spe_colonist` | Gated | special_job_source_file:16_shroud_jobs.txt `common/pop_jobs/16_shroud_jobs.txt` | 648 | `868480705cb8800c79ebce4f976fe186bb68d89783a4a5b455581478cd5928ef` |
| `technician` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/03_worker_jobs.txt` | 102 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `technician_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 2332 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `telepath` | Covered | recipe_imported;capacity_reference_inactive_in_ordinary_context `common/pop_jobs/02_specialist_jobs.txt` | 1400 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `telepath_drone` | Gated | special_job_source_file:16_shroud_jobs.txt `common/pop_jobs/16_shroud_jobs.txt` | 2 | `868480705cb8800c79ebce4f976fe186bb68d89783a4a5b455581478cd5928ef` |
| `trader` | Covered | recipe_imported;availability_requires_runtime_context `common/pop_jobs/02_specialist_jobs.txt` | 2174 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `warrior_drone` | Gated | special_job_source_file:04_gestalt_jobs.txt `common/pop_jobs/04_gestalt_jobs.txt` | 2600 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `wrangler` | Gated | special_job_source_file:14_grand_archive_jobs.txt `common/pop_jobs/14_grand_archive_jobs.txt` | 51 | `8b5bcf1f6c470b765e178fcdad131c28cb7c6dd504609802761357159092a859` |

| Job flow resource | Status | Source | Line | SHA-256 |
|---|---|---|---:|---|
| `consumer_goods` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 2671 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `trade` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 2679 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `minerals` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 2702 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `food` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 2713 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `alloys` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 3455 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `unity` | Covered | `common/pop_jobs/00_other_jobs.txt` | 550 | `62a63b3e17c111ad475b92193eaa80ec727375e95ba3b04b30883d08215e0a3f` |
| `society_research` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 317 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `engineering_research` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 923 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `physics_research` | Covered | `common/inline_scripts/jobs/ethic_job_resources_memorialist.txt` | 9 | `0c08a5df834f71915c677597a86011027aadfb9cf99917905e7b029dc23594ce` |
| `energy` | Covered | `common/inline_scripts/shroud/jobs/colonist_job.txt` | 76 | `14d848d38f7105fa97421b28b359b805ca19c537a77e98f46e46191b64ec9ae2` |
| `nanites` | Gated | `common/pop_jobs/02_specialist_jobs.txt` | 505 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `advanced_logic` | Gated | `common/pop_jobs/02_specialist_jobs.txt` | 3037 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `sr_living_metal` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 3307 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `volatile_motes` | Covered | `common/pop_jobs/02_specialist_jobs.txt` | 3308 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `sr_dark_matter` | Gated | `common/pop_jobs/02_specialist_jobs.txt` | 113 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |

## Read source/dependency files

| Source | Bytes | SHA-256 |
|---|---:|---|
| `common/buildings/00_capital_buildings.txt` | 36196 | `d07e51f3071553ce18a1dac53408805430ec86c8ab0c3f247c3949485b542b10` |
| `common/buildings/00_example.txt` | 5943 | `c1a289972f18146767b49f096b757ad2d0a9a7bb65e90b2a558d167f67bc5595` |
| `common/buildings/01_pop_assembly_buildings.txt` | 36206 | `80751285f789496897610060eae7b7142fe9066838e962c0ad8b7222b0a816ac` |
| `common/buildings/02_government_buildings.txt` | 25260 | `e509612cbbd6e7e8369c05caf8e456ea4fda7b7f18cf677a79812d2d1ade69d0` |
| `common/buildings/03_resource_buildings.txt` | 35093 | `1d0ab107cedb5be2c0de9bfadd9a2b44490bcdee7dc2af184df1580dc02592dc` |
| `common/buildings/04_manufacturing_buildings.txt` | 30950 | `48d9fb1613fd0c32b07c98399441f744464b72c0c902ab0c53749c348e69c44c` |
| `common/buildings/05_research_buildings.txt` | 35423 | `17f2bf908c25b2c357756628340790457d41cdd5a9e2e07a62fb9fbc6d209af5` |
| `common/buildings/06_trade_buildings.txt` | 5193 | `1167e5e2f095ab8c6d330fb5f6d623edc14b281c4a2bdaa6c5b36516fdb544a9` |
| `common/buildings/07_amenity_buildings.txt` | 25439 | `6483b2c11e3b42fb5eb46f5466bcedd774733646dfbdb6c480b1c0504705af1e` |
| `common/buildings/08_unity_buildings.txt` | 71828 | `aa26f94daf90a18fecf53ba37d94156c6d3acee601cec6117738e8d0be9950d1` |
| `common/buildings/09_army_buildings.txt` | 7315 | `f3f847c877e6ce8aa64340c13f059f535105290ec7fe4cca4df71cf0360f780e` |
| `common/buildings/10_deposit_buildings.txt` | 7785 | `2d05fffe2afee8140cf1414192ff324805cfa902e9d418e1b77367e5d1398096` |
| `common/buildings/11_primitive_buildings.txt` | 22336 | `e8139425a63d0a95f4c825f4007a607ca52b31776d51caea09a0aacdd67aeb02` |
| `common/buildings/12_event_buildings.txt` | 14616 | `448723aa2fdb485e3ce3a2ed9acaab80344986d4c55ba3638eda60b3738793a5` |
| `common/buildings/13_fallen_empire_buildings.txt` | 120381 | `76b32ad7b41b56746496b912b0ce10c4fb6da75d4ac907da2be543a3bb4db3fd` |
| `common/buildings/14_branch_office_buildings.txt` | 90644 | `2f6486c3a5db8a38578b017a4680a94eaf701fa217c8d0bd79537c96e9e22fa2` |
| `common/buildings/15_overlord_holdings.txt` | 44303 | `fdc25778d9bf07fe934fb8f70be9c155bfaceb05d3fb80a5a35b7f00808972c0` |
| `common/buildings/16_first_contact_buildings.txt` | 4223 | `3e4390fef7a1ac15129c7755d691e760496a7fa91a44f4f1e497ed997941f95d` |
| `common/buildings/17_paragon_buildings.txt` | 5495 | `37ae0a82575c689ffe54dcce475bf81e1ed3425145effe11f58a369aec647edb` |
| `common/buildings/18_astral_planes_buildings.txt` | 4674 | `287d82d0dff2935e5699c817fd224655fb7c7580fa16eceb44f5c91bd063980e` |
| `common/buildings/19_cosmic_storm_buildings.txt` | 18020 | `d8b5892d26f7ab382377631604498de0dfc72891cd98b3265d2c0ec83962fd19` |
| `common/buildings/20_machine_age_buildings.txt` | 47894 | `13fa652e8d09ddec539c897b68a306b9ff5df0abd09e99872f44c19e8f963358` |
| `common/buildings/21_grand_archive_buildings.txt` | 23154 | `20b24d6558d05f94e21ba0af07adea89d739824f03c0bf311a67947dcec99bd9` |
| `common/buildings/21_wilderness_buildings.txt` | 53885 | `6ff33a66046d559a8e5d5ade5b9a5f6012581d8cb328f187adda197a84193977` |
| `common/buildings/22_biogenesis_buildings.txt` | 6626 | `5b8ea7adf8df561700c260477bc95efeb9883a0df2a62c97b8e415f1d71e4148` |
| `common/buildings/22_extreme_frontiers_buildings.txt` | 2808 | `0b8aaf496361e621308ccb3a7b8df9e8a2d8d01adf2da9ecc51338afdd0fafd5` |
| `common/buildings/23_shroud_buildings.txt` | 20652 | `800e9c3223d23484fc333382763fa8c71819e664b35adb400ecd11c6ee4dc27e` |
| `common/buildings/24_nomads_buildings.txt` | 11846 | `5920cee2af4ca9d0689ed527041e26ef62c2c644b8fa23f57cabcc66f43dd457` |
| `common/districts/00_urban_districts.txt` | 35392 | `4153e3912143dcfa7e6349f7559d4dd71187b4dd0bb4e20c7e298638d6a51737` |
| `common/districts/02_rural_districts.txt` | 12694 | `90e6faea595af0c662dfd4847d37c9af77bbaa4bc3c8c5894f83e26fe02f98ec` |
| `common/game_concepts/01_job_concepts.txt` | 6492 | `71b9060ef10d50da1d063d436cde9435de9b6a2b0c6d86bf3b4940eac5671b56` |
| `common/inline_scripts/buildings/nomadic_cost_switcher.txt` | 201 | `2d63da2b2ab40279a14f201ef09d87a5abc76172c452b9523781e25d3fba3346` |
| `common/inline_scripts/buildings/nomadic_cost_switcher_with_additional_trigger.txt` | 227 | `02f5bc03589f0f354a592983b71c64757d04eca798b0ecb7f69640cd0b9778e8` |
| `common/inline_scripts/buildings/on_all_capital_buildings.txt` | 11229 | `f767c26187ff6716fe6a42b8ca990e4894a8071e31d829be9a77cf5f8fd3e2e3` |
| `common/inline_scripts/buildings/on_all_habitat_capital_buildings.txt` | 882 | `bd7bb80b3969b7e216e2e1d2b2ec680da5532aa916812c6514483423e4afafbe` |
| `common/inline_scripts/buildings/on_all_wilderness_buildings_districts.txt` | 920 | `695eefc74535e634b18f56892585764d7485d4369fab6017c8b9d09a76b13c33` |
| `common/inline_scripts/buildings/planet_job_resource_produces_add.txt` | 493 | `a6472d5aa7db88dc329cf2daf8d9e27439d183137e9a4c4970e532d41819b226` |
| `common/inline_scripts/buildings/regular_empire_capital_jobs.txt` | 712 | `9c69ca39c00dc7d7fa63aa04d654effc5fb5ef42465164e66bc3a3bee04eb6dd` |
| `common/inline_scripts/cosmic_storms/PlanetaryShieldOnBuiltDestroy.txt` | 442 | `2e0dbf34518d4057fe37f48c958bbb7a58190892acec54449d53f1e6bc3d0d18` |
| `common/inline_scripts/cosmic_storms/StormBuildingModifiers.txt` | 236 | `42e8a49122d59ed305fae7f5af481919003c23b134cb992b20e3d5e27fab5fc6` |
| `common/inline_scripts/districts/ai_alloys_extra_weighting.txt` | 181 | `cd835301ddd2dc5205c603e9d0cc4c69f84a90b6057df74daaf56f7bd11358a3` |
| `common/inline_scripts/districts/ai_consumer_goods_extra_weighting.txt` | 189 | `0b43bd747b0561bc5794248f5716c0b5c5c51ef64968ab1128e20243c2c4dcc5` |
| `common/inline_scripts/districts/ai_research_engineering_extra_weighting.txt` | 261 | `bcc50c0f2e90d03c9e0e1af48c4b9a1c6c6f50412a20dca065c607422b8faf54` |
| `common/inline_scripts/districts/ai_research_extra_weighting.txt` | 561 | `ecc53da005d614482df34daed9047acc6a0fb5909759257e46de7172f89589a1` |
| `common/inline_scripts/districts/ai_research_physics_extra_weighting.txt` | 243 | `769fd2445505f11c93f00e3d44893d84ead4a9014437cf1a3bf5cf206c411ff6` |
| `common/inline_scripts/districts/ai_research_society_extra_weighting.txt` | 247 | `43be897a3f02dd12dc13e45282a452e78ad7ced1adeed42f44e2c466168979e0` |
| `common/inline_scripts/districts/ai_urban_district_extra_weighting.txt` | 1076 | `fa3a8ae6b38702ef00ee4b03e1780d1dd80082cdd17637313fdbe7effec015a9` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_farming.txt` | 891 | `0a93d51950c9f34cd1ac1f86a8ce627b5fdc666cd7978d7e6a76e1dbc3dcd4aa` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_farming_anglers.txt` | 1080 | `fddd1f9588dfa3682e182f7ae11d6b2a8ec7a077d0f51a1e8318afdfdfc20b89` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_farming_default.txt` | 939 | `0aeae816a2038fac89d8a17433bf6fe4f7cda3e632b9ecbc23a73f728f5238c1` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_farming_exotic_gases.txt` | 835 | `03d9c6f74d9ec98249e3914d10e5b9801d875915d39de2637382529a8aef0ffb` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_generator.txt` | 465 | `99b4d9aedc37184295143225cfbfca384e8a81419986fa4a24e7ea2fb8a5a911` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_generator_default.txt` | 933 | `57752756a9ab717a9345338dde6b53f1a479b0e6b70647300fa7fa72954cff4c` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_generator_volatile_motes.txt` | 823 | `950ac4f3c8b43ab9e6ebf699936a3167864d5c7af35fd1b90cfc4a590bb37ce3` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_mining.txt` | 936 | `6ed3fc7591f70641ca8f806aa184c59b8caecdaf897bf0285b113578ec814cd7` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_mining_betharian.txt` | 778 | `02cef8352f4c337affd0d002790d73c90c66f59829b1763eb71f5ec4297528fe` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_mining_default.txt` | 805 | `881371cf1803dd5ef0c4ca801badc52e377b599ac21d244a06553b7d050a625f` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_mining_rare_crystals.txt` | 810 | `5775aacee1e75fa95a67626a05e73d99e0d44dcd547fac51f0143f7c096ac1cb` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_urban.txt` | 590 | `42ba1fa35e67228bf1e408c483b5839adaeeaa0f63975ea4c852e66ea6628303` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_urban_default.txt` | 6690 | `174b513b5e83b5c654f39a6eff8d295a1d18988092887faa9c850bdbbf8159f3` |
| `common/inline_scripts/districts/district_triggered_flavor_desc_urban_spiritualist.txt` | 3188 | `af6922080e9d53c965fd29e547ad1c0f272155f055abc13aa1a711b70a552652` |
| `common/inline_scripts/districts/district_triggered_name_farming.txt` | 965 | `b43ef436ac2b4564184b59e6b58157a3246349caf318990ced72655dcc98a7cc` |
| `common/inline_scripts/districts/district_triggered_name_farming_anglers.txt` | 1020 | `2992e623e5db07c901b1a5f46e16872d9bf8284e7bdc1fb08b0dc99845ca1f23` |
| `common/inline_scripts/districts/district_triggered_name_farming_default.txt` | 803 | `da76056f3691587fa1bef1175b98a428f3b376557f39ab2d4198e310db2b51bc` |
| `common/inline_scripts/districts/district_triggered_name_farming_exotic_gases.txt` | 787 | `a1c476f5290fff02c41e1ba12ebf0a6f675f9ba56c2e2ca8f01dca4df45dd877` |
| `common/inline_scripts/districts/district_triggered_name_generator.txt` | 746 | `3c3f95fcb680abf5fb085a94c68c0ca114db54531b46495b9fbef9b923c79ce7` |
| `common/inline_scripts/districts/district_triggered_name_generator_default.txt` | 780 | `2942fd00b24ab9be612fb91fdeceb9299dfcd2e29bb062cedb35db49da1d6ac7` |
| `common/inline_scripts/districts/district_triggered_name_generator_volatile_motes.txt` | 774 | `3fba8e9b3c50c513331b4833daec7f52ebe29c2695a83dddcd74739e3d9bc1e8` |
| `common/inline_scripts/districts/district_triggered_name_mining.txt` | 1011 | `d510cbe5dedacf7998fabc7a90a30577f87006558f79408c2655baf05d02f6d5` |
| `common/inline_scripts/districts/district_triggered_name_mining_betharian.txt` | 730 | `cc060345d4636931b00a1a14bdaacb64dd75b9d8d86502bd46243e24fa14ea0f` |
| `common/inline_scripts/districts/district_triggered_name_mining_default.txt` | 769 | `8e8c47743beebdd6a1d607371a4a59de5701968ccbef205d1ae1acf53d1e7dd3` |
| `common/inline_scripts/districts/district_triggered_name_mining_rare_crystals.txt` | 762 | `fff00368729461e5dcfd9c141dc3208199fd6e26f254116bbcfa52f49a8590c2` |
| `common/inline_scripts/districts/district_triggered_name_urban.txt` | 596 | `d14ffb3197044501a5f058ffa71acbb9478e6bc475d02b03ac3582ef80fe6335` |
| `common/inline_scripts/districts/district_triggered_name_urban_default.txt` | 6313 | `17209069919adebb396b0474d8f3132d62d05de76d90fcb0da52ad20c0987a40` |
| `common/inline_scripts/districts/district_triggered_name_urban_spiritualist.txt` | 3515 | `e72ff8dad3d5dd82dad9829f7e575af42a9823b7dd6341ed9c1e1d390838d99c` |
| `common/inline_scripts/jobs/academia_recruiter_naval_cap_add.txt` | 221 | `3e11a70ccb0397d0a159bb0451daba62f3160605121bd114252561e495f34414` |
| `common/inline_scripts/jobs/automodding_priority_alloys.txt` | 92 | `77bb2fa89abd52e4c2c26ba20361c72ac24c88c69cebf3c89b851ebe258be8a7` |
| `common/inline_scripts/jobs/automodding_priority_charisma.txt` | 183 | `d4d36810b66fb9d60b24e84b249c65286bd32aaca41f9a57050f02c9bf42f978` |
| `common/inline_scripts/jobs/automodding_priority_consumer_goods.txt` | 188 | `5a7284e625d828ac8c4e78fdc4da7d72c22dc7cba6dc3dc2ddd377dd1ab14391` |
| `common/inline_scripts/jobs/automodding_priority_energy.txt` | 182 | `b5149c230b194e455d11e0cd0c6448f837ac554ce7157ca62483201d82100ba7` |
| `common/inline_scripts/jobs/automodding_priority_food.txt` | 158 | `dab45115f57b6b758730b33ee9cde863589b186f3e2fce668caf28e916eb4fcd` |
| `common/inline_scripts/jobs/automodding_priority_minerals.txt` | 173 | `cfc70242717cd04417ab47be6c2a361b9f2784cfd816b8f6988fbb23aa9617eb` |
| `common/inline_scripts/jobs/automodding_priority_research.txt` | 192 | `208b26e9a3d26fb9a0ea43eb78ae052acd16c9a3e50ebd6edb1808da3019a9e9` |
| `common/inline_scripts/jobs/automodding_priority_trade.txt` | 285 | `21c9a44ffdb7d9b665dfb8a56b8dec9482062052af1c72bbec3a6e3879202955` |
| `common/inline_scripts/jobs/automodding_priority_unity.txt` | 217 | `f96eebab565e647dedd365397320d548364bd7e25769a91f3be79306e723ee11` |
| `common/inline_scripts/jobs/biologists_add.txt` | 528 | `205000ff541b683737dd53b80b07862354cd5d630da45b97491898bb933a6e2c` |
| `common/inline_scripts/jobs/clerks_add.txt` | 439 | `56b391d91d99a0a6ea5d45e536445d793a2d46dd04e410f04dcb68ed57e9d16a` |
| `common/inline_scripts/jobs/enforcers_add.txt` | 396 | `4615a8fc49dad8490daa1b63dc3deaa9b4dd4eda7825ff2a11ce1dc53e7bbddb` |
| `common/inline_scripts/jobs/engineers_add.txt` | 526 | `081bfbcd0e2f9f550572d63d429273f006df668069b7be5fffa5e11bd7ff8ec9` |
| `common/inline_scripts/jobs/entertainers_add.txt` | 470 | `2cbdbbf627663235cefcce504860211a46568c2abd6213650d7c43b7b2a9cf21` |
| `common/inline_scripts/jobs/ethic_job_modifiers_memorialist.txt` | 1271 | `2180dc4049f9ce3fd9fc1cc859414cc761373d513686915b985c358d29f50766` |
| `common/inline_scripts/jobs/ethic_job_resources_memorialist.txt` | 696 | `0c08a5df834f71915c677597a86011027aadfb9cf99917905e7b029dc23594ce` |
| `common/inline_scripts/jobs/factory_add.txt` | 305 | `9e19b179dc30379dbc67be98704b7fa3f54f899fd0538d81df3688937514870c` |
| `common/inline_scripts/jobs/farmers_add.txt` | 591 | `207ed2b695ecf3f31930d66562866357c760901664bc9e8d9c8be6aff0d90a13` |
| `common/inline_scripts/jobs/foundry_add.txt` | 274 | `10266b043c782bb14d95163523d51c445c57d2172f2b32cf2bee79b504a1c392` |
| `common/inline_scripts/jobs/healthcare_add.txt` | 134 | `9d5ef2df28e45a9ac6e85d7e1380f0ce74d2ed8a1f312974255eb2207bb80060` |
| `common/inline_scripts/jobs/inf_navalcap_jobs.txt` | 241 | `25038d9b97dbe108fe25091038f907aa8ee6282a71fd433f7122bbbaf6c49bb2` |
| `common/inline_scripts/jobs/job_enforcer.txt` | 514 | `3a7d1661bfe3acefef48d1db57ee3a908e40fabec5fc86ad7767d590bfb7fcdf` |
| `common/inline_scripts/jobs/job_enforcer_additional_modifiers.txt` | 1084 | `a44e311c25f5e83716e10a2397b5c1374db2dfb462cb6260d12903085176b4b3` |
| `common/inline_scripts/jobs/job_enforcer_triggered_resources.txt` | 339 | `6c64fc823b0b3ee4a0848dd782ecde88d6eb09e24318baf7d76f83ca42f8590e` |
| `common/inline_scripts/jobs/job_telepath.txt` | 429 | `337b9fbac2cc11cc4ced3e7cac071bf47ef5c0b154767d372fc8d23308657b9e` |
| `common/inline_scripts/jobs/job_telepath_additional_modifiers.txt` | 7316 | `9f04ff2fca96998fc2419b60c8f524ba84be1afb2f381d4d17560d158b753aa5` |
| `common/inline_scripts/jobs/job_telepath_resources.txt` | 1173 | `df47f959af2d70ac8448725c3a36b186937d9ce387e09ac07f5a5f6c7dc9163e` |
| `common/inline_scripts/jobs/miners_add.txt` | 633 | `251a8d0feee675e5ea406a369d1a3377c0ee6470b1a0251465291948e9ae410b` |
| `common/inline_scripts/jobs/mortal_initiates_add.txt` | 68 | `f3da66c39089875ccc55870185ce1ff3279b810e0926839bda229650777ff1fc` |
| `common/inline_scripts/jobs/physicists_add.txt` | 528 | `bdb528f49a2e32c14c74ddb613fded80580ab478d0d8c9e88558130c7de2cc17` |
| `common/inline_scripts/jobs/politician_add.txt` | 446 | `061f4dc53ffd04f0385f0a02f4b7596bcea18caee16eeb6bc8cc1d9efa1ebe97` |
| `common/inline_scripts/jobs/politician_add_from_civic.txt` | 143 | `7436919ed2e1a8e087530981b8574057024bf762f1fd90517d3796683d88ef82` |
| `common/inline_scripts/jobs/priests_add.txt` | 1175 | `35ef0f8a8f3c100dd7c8c1d9b7a61545d8680d92306c7e56a5bf0a73bb7f99d9` |
| `common/inline_scripts/jobs/researchers_add.txt` | 973 | `9eeec59bb74326909ab30041dc5080e1ea4a717f1ae746471b42953cf02894d8` |
| `common/inline_scripts/jobs/roboticist_add.txt` | 268 | `5f35c53ecd6ff100d8a495c9a8a4224e0c84acb52a40a3e5b6fa2415b90bc082` |
| `common/inline_scripts/jobs/soldiers_add.txt` | 734 | `22f4125657a639f6b961cc034402eb17faec2b6b15cb0ea95260e6a8d1b55135` |
| `common/inline_scripts/jobs/technicians_add.txt` | 588 | `9fde1fb16d7d11ee29469a9d4301532aac5fdc3134fb69da88d5b5262ea5fde7` |
| `common/inline_scripts/jobs/telepaths_add.txt` | 610 | `3a45f30857fb1da9904f3b3bf13c0685c770913d1d31226893d4ee428ecb51b9` |
| `common/inline_scripts/jobs/trader_add.txt` | 657 | `f62c1b4a952b1e46f0cc01c2aa34444fadc34b79d37d6782b744dc0f6b7f0da6` |
| `common/inline_scripts/jobs/unity_jobs_add.txt` | 284 | `27802be28cc0b70ccadc3d1d8e0284f3226fca55d9280e974191804353bd5562` |
| `common/inline_scripts/jobs/wranglers_add.txt` | 270 | `200504b073920ccadd3caee4bc4c363aedbf6ef820a53510dd685257aed04efd` |
| `common/inline_scripts/jobs/zone_biologists_add.txt` | 11045 | `2d452f7fe5e24374441bf2815771c5be5b9aa69397cbd895aeaf31ed7ae9459a` |
| `common/inline_scripts/jobs/zone_engineers_add.txt` | 11284 | `5b6434011cb59243527925ccf0c03389b03f3b000d31dcf2f8f637108717fff7` |
| `common/inline_scripts/jobs/zone_factory_add.txt` | 2239 | `0c765df0282335ee4cf74e0e8876ce584f18466650702ead7f847642ac9125b1` |
| `common/inline_scripts/jobs/zone_farmers_add.txt` | 3373 | `48a9f02b30ff9cfc6986a56118cd67aba6490fee72c75bb5cecd729fa11c1734` |
| `common/inline_scripts/jobs/zone_foundry_add.txt` | 2426 | `76a0376e41fa879774ada460893a3dafe1ea349166d076143e08f2b72a516d88` |
| `common/inline_scripts/jobs/zone_miner_add.txt` | 1869 | `5c3258eb1420f3ce2777d51b3e063579fd4986c84ad7b128d2193f9a9ebaf6e4` |
| `common/inline_scripts/jobs/zone_physicists_add.txt` | 11356 | `079693c7f30fdafaff525c50480b5caa07a5b1e0abb9c7ed49920d32bba5f2ad` |
| `common/inline_scripts/jobs/zone_researchers_add.txt` | 13872 | `19a65bd577add8ffc78c51d9fb40ef83e367a234fa60cc21fceda176cbfe613e` |
| `common/inline_scripts/jobs/zone_soldiers_add.txt` | 3935 | `5778f337543cb4b9284cdee2aa456e770edfb8682a83952aa7c48de319bf2213` |
| `common/inline_scripts/jobs/zone_technicians_add.txt` | 3451 | `1fc07675e7e1594cd228f45369476e6bdd929e4b290be4789a998ab2fe1170b0` |
| `common/inline_scripts/jobs/zone_trader_add.txt` | 4399 | `e8fde8e6d7adc9b25ec31afbfe49cb1515d3955c753a1fa1d7f67d894278469a` |
| `common/inline_scripts/jobs/zone_unity_jobs_add.txt` | 6190 | `643c811c20ff5015e256a3aa0bfd09c16d70ae230f179d0c5276f2941c1381df` |
| `common/inline_scripts/output/bonus_pop_growth.txt` | 403 | `f104817e5d734b28007061a8f3adc33243f687218484df5f33f21c12692b5a93` |
| `common/inline_scripts/output/colony_support_contract_modifiers.txt` | 533 | `0ca4f4a4934177fe6a513795ba999808aa09ae94dad551e91042d8349b484d1a` |
| `common/inline_scripts/output/colony_support_contract_production.txt` | 458 | `cf408f91bf2154a6ba48f555cdce931bf2e3b1215500bd12d2d7c44828966016` |
| `common/inline_scripts/output/factory_output.txt` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `common/inline_scripts/output/foundry_nomad_triggered_modifiers.txt` | 220 | `fb8176135083d301a2ebffe1ce5e41e235633ff9b0cf2ebd5271217eeafac9c8` |
| `common/inline_scripts/output/foundry_output.txt` | 1222 | `872d1c29a4242c4847d455e00638fa2131582ba2e7808848d312072def677796` |
| `common/inline_scripts/output/genomic_triggered_modifiers.txt` | 720 | `51a3e05716c3fd5a611b64ee0c96b94ecd2f094c8731f707e3098c38d419653b` |
| `common/inline_scripts/output/healthcare_triggered_modifiers.txt` | 908 | `f4926b0d4773bcbcb0fe2118e73fdd73dcbf0da547b06289b11da73164c5b8d8` |
| `common/inline_scripts/output/researcher_output.txt` | 0 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `common/inline_scripts/output/soldier_triggered_modifiers.txt` | 2668 | `e84be3182cd3bdc5a196147e12ffdd71a9ea466219d2815abdf6601f7e242703` |
| `common/inline_scripts/shroud/jobs/colonist_add.txt` | 286 | `8a29ba40ac312a7fa1d142027f64c502778a5f9a3a682a46321e5368d0c8e268` |
| `common/inline_scripts/shroud/jobs/colonist_job.txt` | 1687 | `14d848d38f7105fa97421b28b359b805ca19c537a77e98f46e46191b64ec9ae2` |
| `common/inline_scripts/zones/potential_no_wilderness.txt` | 116 | `43758b11f23196355883082e49b5df1763b1109fc75fa496bc3b81a91821cb2e` |
| `common/inline_scripts/zones/shared_betharian_zone.txt` | 902 | `ae08cc597f8e3f2c8ee164c79296f7cada9783538b4582a582e6ab5399bd4404` |
| `common/inline_scripts/zones/shared_city_non_urban_zone_modifiers.txt` | 92 | `fe613595b2109be9f38738b225ef0d57a8c8e1cbd2928363c4e4c5cb2001b397` |
| `common/inline_scripts/zones/shared_energy_zone.txt` | 818 | `9e78fc6884c3dadf7e451b486366d408c723ff7f818377e856878f6b3ceb9ef2` |
| `common/inline_scripts/zones/shared_exotic_gases_zone.txt` | 1169 | `31f49f3daf6fb0624161eb9fe401a01891a46faecef0ad14e118b467e878d5eb` |
| `common/inline_scripts/zones/shared_food_zone.txt` | 999 | `176ffc400abf4d659185f95ed93eb9413f4646d587ecf1c30e3f2a8edfa520ac` |
| `common/inline_scripts/zones/shared_fortress_zone.txt` | 727 | `389d52d22062cffcea766e9a9b5d0ec793cb8c419c2c270ec9d936751a98496a` |
| `common/inline_scripts/zones/shared_industrial_factory_zone.txt` | 835 | `c88aa3472a5bab3e4ab559302fe6c7fc9ae86a579d0f8e3f6537917abfc0c275` |
| `common/inline_scripts/zones/shared_industrial_foundry_zone.txt` | 868 | `355351b6e6b151e1ff1f6bbf22b72977acc850870f907abe0231362b74137d95` |
| `common/inline_scripts/zones/shared_industrial_zone.txt` | 983 | `0d69078b40e41304577b96816dd66631ddda90e45a2305ca5fd3c6345d578d6d` |
| `common/inline_scripts/zones/shared_minerals_zone.txt` | 811 | `9a755b126381e7b49373b4dbdd7aae5a505a37ff46bdf4671b20cf82e31ebbc5` |
| `common/inline_scripts/zones/shared_rare_crystals_zone.txt` | 1162 | `ffef72ed5aad0b444749933691a7fcd1429788a075b4e4d3402042a9a766680b` |
| `common/inline_scripts/zones/shared_research_engineering_zone.txt` | 615 | `d86f675bdc77ff9b4f7e3ba6af4e6b7e25e455b03f21f0146b07e30ff0fa3071` |
| `common/inline_scripts/zones/shared_research_physics_zone.txt` | 608 | `736403180ae8c87d335342dec198efd83bd4a487694521519c4be3a0b878738e` |
| `common/inline_scripts/zones/shared_research_society_zone.txt` | 608 | `18ef6f449b3f79676493ad64292459bef094fc293d05d508edc3bce2051ace5d` |
| `common/inline_scripts/zones/shared_research_zone.txt` | 905 | `0fcd0f04f990126f82c95aa07682d1d5391725b5a55896a55e0eda48513380ea` |
| `common/inline_scripts/zones/shared_research_zone_modifiers.txt` | 194 | `389120cfdb085a03fe40480c806f43d8e5ac58eef33bac34f15ac6042e7f1fa8` |
| `common/inline_scripts/zones/shared_trade_zone.txt` | 757 | `b91261b7c4a584358dd970854a077d25f55af1411050cc18c2ec499c82d02af2` |
| `common/inline_scripts/zones/shared_unity_spiritualist_zone.txt` | 845 | `9fe95e0f096b375de631571657c6952bd8a29c1e26dfe7d83ea05d9e754d21bc` |
| `common/inline_scripts/zones/shared_unity_zone.txt` | 1071 | `1b39b804ba4fb95cfaa99af66ee13a1067e9e5e824f61115c5140a0ee3b17bc2` |
| `common/inline_scripts/zones/shared_volatile_motes_zone.txt` | 1202 | `ec2eb80b95a3021ba34cc4526673b01a164ad3884648c0cd89a86e2d8c3a58ad` |
| `common/pop_jobs/000_pretriggers.txt` | 1603 | `5cc9b13bd8b8a81468e5de18fb01095d946a6bb3cd8336a728f877d0d5875f80` |
| `common/pop_jobs/00_other_jobs.txt` | 35045 | `62a63b3e17c111ad475b92193eaa80ec727375e95ba3b04b30883d08215e0a3f` |
| `common/pop_jobs/01_ruler_jobs.txt` | 13716 | `58a4b8595e692216e2c4faba8fbf29975880fe0bfb18cb4bd5e257d35877331f` |
| `common/pop_jobs/02_specialist_jobs.txt` | 70358 | `3e35067fd9b2524a101c2cc35dbbc3c67b383a46c8bf3bbfe475da77edbf6fbc` |
| `common/pop_jobs/03_worker_jobs.txt` | 21119 | `a86f096d6ac18dd5ee3a6267d31551c933816300e5ccd279f5e7b6afb5fe18df` |
| `common/pop_jobs/04_gestalt_jobs.txt` | 55976 | `9469c178efbaa2d8e5bce43545ee6d5a184ee0e764336e85170795d0cc6820ad` |
| `common/pop_jobs/05_primitive_jobs.txt` | 38144 | `1ac9b220281ad62ee88988bc25e77947d9f58802c668b3930cfcb93d7150fc2e` |
| `common/pop_jobs/06_event_jobs.txt` | 43034 | `818f6c57f8ee39ff7a4892b41939b7dcef1b132681571c57ecc1336ba3d2cfc3` |
| `common/pop_jobs/07_fallen_empire_jobs.txt` | 11626 | `6c86bcdcc5f25b87027d72a561f9623ecd4ca1d4ba02b391c6004756322a5064` |
| `common/pop_jobs/08_overlord_jobs.txt` | 43292 | `fecfd6c415afa11fec353863f3b96079d91c6c7a32130991911d7837253c231a` |
| `common/pop_jobs/09_first_contact_jobs.txt` | 6643 | `ba2747e8ddbac4b08984355877ed0de31d0bdf184bd73a9f1dab1e2c63590236` |
| `common/pop_jobs/10_paragon_fake_jobs.txt` | 1837 | `99ef34b0fa0d8d7690ae5b90159fa4259679375f2550eb0647e4d8d7e20ba142` |
| `common/pop_jobs/11_astral_planes_jobs.txt` | 8133 | `537982c6ce9647618b9ec22f91d1ea2f43f63b1619c0bc1bdaa7c3ad94ac21a3` |
| `common/pop_jobs/12_cosmic_storm_jobs.txt` | 774 | `abfbfb76c00c849a5e1001348c4ac5f82f7d84bbba9a94227d1e2016f2776131` |
| `common/pop_jobs/13_machine_age_jobs.txt` | 16997 | `5b4963303f53e7fe5d654c565e4aadd9aec3311787e0a3ff468afc8b6b01c1ea` |
| `common/pop_jobs/14_grand_archive_jobs.txt` | 7349 | `8b5bcf1f6c470b765e178fcdad131c28cb7c6dd504609802761357159092a859` |
| `common/pop_jobs/14_wilderness_jobs.txt` | 67 | `ca2fa3cda3bce6e3caa052b56ab51454a7f0a06fbd09b1a58fa90244c3ff17c3` |
| `common/pop_jobs/15_biogenesis_jobs.txt` | 7254 | `183ab971408870ae21624f9efe485a63831e861c32b72caab64b01fb02c98d31` |
| `common/pop_jobs/15_strange_worlds_jobs.txt` | 3072 | `3e8b251cd104815329f942cc649bb8a07ff24a7a88ce882f6d729c5ca70f39ee` |
| `common/pop_jobs/16_shroud_jobs.txt` | 14923 | `868480705cb8800c79ebce4f976fe186bb68d89783a4a5b455581478cd5928ef` |
| `common/pop_jobs/17_nomads_jobs.txt` | 13051 | `74f27999909748943c88e1bd40edc9e6f1185568a4ea5ad5626d5df63de3d0c8` |
| `common/pop_jobs/99_swap_jobs.txt` | 749 | `547eee0d83d9cce19dc0986a1c9fdbd86fb1facd79bbb50f7bc53e9db39c8e4d` |
| `common/scripted_variables/00_scripted_variables.txt` | 20038 | `d920a5a9c98c3c0926cd3852d8b4869e8492a9b211bb48e5cc023b2d416421be` |
| `common/scripted_variables/01_scripted_variables_jobs.txt` | 164 | `cfcc67498d34b27234d32dc1dbaee64c3a68a78b357c86c946358ffd8a0562ba` |
| `common/scripted_variables/01_scripted_variables_megacorp.txt` | 2574 | `1b5d81fd640d580197c6bd597cbac8587bc461d95a3ebbafd5b21e62279e356f` |
| `common/scripted_variables/02_scripted_variables_component_cost.txt` | 22041 | `d2b035df63e70f0ae970f03c06c70ee6b6f52ef372e049b6c69a50517d183d07` |
| `common/scripted_variables/03_scripted_variables_ships.txt` | 12351 | `44ce49d52ad1d5d127149ed61b6c5ee497eca873f5836c7787946e75e11d97dc` |
| `common/scripted_variables/04_scripted_variables_overlord.txt` | 2395 | `5949380ad5a5a9a3dfeaff7cd887c8a468eafeb14a030e13c7a6829d621066ae` |
| `common/scripted_variables/05_scripted_variables_first_contact_dlc.txt` | 890 | `0eac4a9cc2157c5cca153bcf17c54243c362b8f6265a5431032b252cbeb566c0` |
| `common/scripted_variables/05_scripted_variables_paragon.txt` | 5555 | `22c232679a4415bd2c32c409ed6afd86fdd2f2d00a5ddad7d51682400603de33` |
| `common/scripted_variables/06_scripted_variables_astral_planes.txt` | 1239 | `615093fd79e194ec1311053df0eb28e0ad501de5f8e52027d7c56e106f1d1a4d` |
| `common/scripted_variables/07_scripted_variables_cosmic_storms.txt` | 2066 | `b22177ede6bc47ae5dc48c2b66bc7b331756da6a35e238b6ba769cef0bc6ad87` |
| `common/scripted_variables/07_scripted_variables_grand_archive.txt` | 6474 | `a93d6953e675978e46bc606c412c67a013c222fe6127f647adbdf435d513e711` |
| `common/scripted_variables/07_scripted_variables_machine_age.txt` | 2242 | `78105faf3570f3bd8f30cd8a470df00004552be3828f2b8899ffa0ceeb8cbaf0` |
| `common/scripted_variables/08_scripted_variables_biogenesis.txt` | 7054 | `2e1ce1d88d4d0f5e6bb0bf84241d24f557e5797101ca9dae18c1b1596983a506` |
| `common/scripted_variables/08_scripted_variables_governments.txt` | 738 | `a079d1174f8ed9278aa7cfbca42f8c233567a681d20d55bd6cce562c9af3410b` |
| `common/scripted_variables/08_scripted_variables_strange_worlds.txt` | 363 | `c4c14aee943b23c729c4ae1721d41a73f2e5b38d315ac7672f696795de2e105c` |
| `common/scripted_variables/09_scripted_variables_extreme_frontiers.txt` | 189 | `3f869ff9910b0309afeca1aa2b49005985b2b1fe0819b1738e44d98aba05da68` |
| `common/scripted_variables/09_scripted_variables_focus.txt` | 602 | `0bfe07737b691c78b09f317751d16a46683546cc3603ad7ff4bbce87799ea26a` |
| `common/scripted_variables/09_scripted_variables_infernals.txt` | 1261 | `2d22619e51c886c8beb808bf76fedcf0c4f1e9c14114d448753849e928fa9c5e` |
| `common/scripted_variables/09_scripted_variables_shroud.txt` | 9454 | `fb748bc44777708a4bfc6a58830c1ed5ea52601fa558814dca60389cf2134385` |
| `common/scripted_variables/100_scripted_variables_colony_types.txt` | 166 | `65e670025ff3961758a4cfc39dd6586921c3ab6eb0a824e65be943b651fe7f37` |
| `common/scripted_variables/100_scripted_variables_zones.txt` | 2227 | `f67b8c2d1d53c5b392ae60d0b89d069b581e59dee52f8102a2a47c430b216155` |
| `common/scripted_variables/10_scripted_variables_nomads.txt` | 17129 | `3c7e83f11ac6016aac72bbddbd4b58489d692691bee6e2447df9ba0d20fbe748` |
| `common/scripted_variables/10_scripted_variables_situations.txt` | 193 | `1cddd88d4d24b8be3db8a428937f2ead072d9b1e2c7479be7273d0d213614d68` |
| `common/strategic_resources/00_strategic_resources.txt` | 9078 | `140dea921a77f76726f361eca99f59f0c7755e4e728dec4699d3b732457911a4` |
| `common/zones/00_zones.txt` | 34022 | `ce48f6aea3b5a5a30dabe15b46b5a32ac9149389b3d412d7f0cccf7f1562dcaa` |
| `common/zones/01_habitat_zones.txt` | 4075 | `567e1aba03ebe7c8c89492fc945a1270ce9d3e1e0deeb471260d90bfa597283e` |
| `common/zones/02_special_zones.txt` | 3314 | `54010b2dd802f96a0634106e5cbe682b4eb7d7143ed0f2bcb3930e69a2a63fd4` |
| `common/zones/03_wilderness_zones.txt` | 7703 | `b5e8fb395812c8624fdc7c2cf38031d5a385a863eaf114c05afb2e4256020d87` |
| `common/zones/04_secondary_zones.txt` | 23022 | `c83accc9ab1e617be3b9f263dfea401725039213cdfa2ca6468c44fbf15a8a25` |
| `common/zones/05_arkship_zones.txt` | 19640 | `d2be3a37e6d39f65aee4ec014dcaa6ed682c1496c5319f3dd94c7efb5de5c615` |
| `common/zones/99_HOW_TO_ZONE.txt` | 6557 | `cbaf30a97b0386254fa738871122e235f7da48f7d2ea9969e46f2d706219a94d` |
| `launcher-settings.json` | 1637 | `3477f427c2ca93c8eae4787a315c67eff2c741be318d2ec1c07ccc43c236d559` |
